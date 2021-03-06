﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using System.IO;

using MEFEditor.Drawing;
using MEFEditor.Analyzing;
using MEFEditor.Analyzing.Editing;
using MEFEditor.Analyzing.Editing.Transformations;
using MEFEditor.TypeSystem;
using MEFEditor.TypeSystem.Runtime;
using MEFEditor.TypeSystem.Transactions;
using MEFEditor.TypeSystem.DrawingServices;
using MEFEditor.Interoperability;

using MEFEditor.Plugin.GUI;
using MEFEditor.Plugin.Drawing;

using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;

namespace MEFEditor.Plugin.Main
{
    /// <summary>
    /// Implementation of Enhanced MEF Component Architecture Editor behavior.
    /// </summary>
    class Editor
    {
        #region Private members

        /// <summary>
        /// Services used for interconnection with visual studio.
        /// </summary>
        private readonly VisualStudioServices _vs;

        /// <summary>
        /// Manager of GUI used by editor.
        /// </summary>
        private GUIManager _guiManager;

        /// <summary>
        /// Loader used for loading assemblies into <see cref="AppDomain" />.
        /// </summary>
        private AssemblyLoader _loader;

        /// <summary>
        /// Machine that is used for composition point analyzing.
        /// </summary>
        private Machine _machine;

        /// <summary>
        /// Settings used for machine.
        /// </summary>
        private MachineSettings _settings;

        /// <summary>
        /// Content drawers that were loaded through extensions.
        /// </summary>
        private ContentDrawer[] _contentDrawers;

        /// <summary>
        /// Imported factories for connectors indexed according to 
        /// their align enum ordinal number.
        /// </summary>
        private ConnectorFactory[] _connectorFactories;

        /// <summary>
        /// Imported join factory
        /// </summary>
        private JoinFactory _joinFactory;

        /// <summary>
        /// The exported general drawing providers.
        /// </summary>
        private List<GeneralDrawingDefinitionProvider> _exportedGeneralDrawingProvider = new List<GeneralDrawingDefinitionProvider>();

        /// <summary>
        /// Transaction used for handling changes.
        /// </summary>
        private Transaction _changesTransaction;

        /// <summary>
        /// Transaction used for handling project adding.
        /// </summary>
        private Transaction _projectAddTransaction;

        /// <summary>
        /// Transaction used for handling solution opening.
        /// </summary>
        private Transaction _solutionOpenTransaction;

        /// <summary>
        /// Current result of analysis.
        /// </summary>
        private AnalyzingResult _currentResult;

        /// <summary>
        /// Arguments of current result of analysis.
        /// </summary>
        private Instance[] _currentArguments;

        /// <summary>
        /// Error of last analysis run if any, <c>null</c> otherwise.
        /// </summary>
        private LogEntry _analysisError;

        /// <summary>
        /// Determine that transactions should be displayed in loading.
        /// </summary>
        private volatile bool _showProgress;

        /// <summary>
        /// Watches used for filtering too often progress changes.
        /// </summary>
        private Stopwatch _progressWatch = new Stopwatch();

        #endregion

        /// <summary>
        /// GUI used by editor.
        /// </summary>
        internal readonly EditorGUI GUI;

        /// <summary>
        /// Runtime used by editor.
        /// </summary>
        /// <value>The runtime.</value>
        internal RuntimeAssembly Runtime { get { return _loader.Settings.Runtime; } }

        /// <summary>
        /// Transaction available in current domain.
        /// </summary>
        /// <value>The transactions.</value>
        internal TransactionManager Transactions { get { return _loader.AppDomain.Transactions; } }

        /// <summary>
        /// Initialize instance of <see cref="Editor" /> which provides editor services to user.
        /// </summary>
        /// <param name="vs">Services for Visual Studio interoperability.</param>
        internal Editor(VisualStudioServices vs)
        {
            _vs = vs;

            GUI = new EditorGUI();
        }

        #region Initialization routines

        /// <summary>
        /// Initialize editor - extensions and environment is loaded, event handlers hooked.
        /// </summary>
        internal void Initialize()
        {
            _settings = new MachineSettings(true);
            _machine = new Machine(_settings);
            _guiManager = new GUIManager(GUI, _vs);

            //try to load users extensions
            Exception loadingException = null;

            try
            {
                //install recommended extensions if required
                if (Installer.NeedInstall())
                    Installer.Install();
            }
            catch (Exception ex)
            {
                loadingException = ex;
            }

            try
            {
                loadUserExtensions();
                _settings.Runtime.BuildAssembly();
            }
            catch (Exception ex)
            {
                loadingException = ex;
            }

            //try to initialize gui manager
            try
            {
                var factory = new DiagramFactory(_joinFactory, _connectorFactories, _contentDrawers);
                _guiManager.Initialize(_loader.AppDomain, factory);

                if (loadingException != null)
                {
                    //we can report failing loading now
                    var entry = _vs.LogException(loadingException, "Loading user extensions failed with exception");
                    _guiManager.DisplayEntry(entry);
                }
            }
            catch (Exception ex)
            {
                loadingException = ex;
                _vs.LogException(ex, "GUI initialization failed");
            }

            //hook handlers if loading was successful
            if (loadingException == null)
            {
                hookHandlers();
                _vs.Log.Message("EDITOR INITIALIZED");
            }
        }

        /// <summary>
        /// Hook all event handlers that are used for editor interaction.
        /// </summary>
        private void hookHandlers()
        {
            _guiManager.CompositionPointSelected += requireRedraw;

            //hook transaction handlers
            Transactions.TransactionOpened += (t) => { _vs.Log.Message(">> {0}", t.Description); tryShowProgress(t); };
            Transactions.TransactionCommit += (t) => _vs.Log.Message("<< {0}", t.Description);
            Transactions.TransactionProgressChanged += (t) => tryShowProgress(t);

            _loader.AppDomain.OnLog += logHandler;

            //hook changes handlers
            _vs.FlushingChangesProgress += (fileName) => _changesTransaction.ReportProgress(fileName);
            _vs.AfterFlushingChanges += () => _changesTransaction.Commit();
            _vs.BeforeFlushingChanges += (description) =>
            {
                if (description == null)
                    description = "changes";

                _changesTransaction = _loader.AppDomain.Transactions.StartNew("Registering " + description);
            };

            //hook solution handling
            _vs.SolutionOpened += _vs_SolutionOpened;
            _vs.SolutionOpeningStarted += _vs_SolutionOpeningStarted;
            _vs.SolutionClosed += _vs_SolutionClosed;

            //hook project handling
            _vs.ProjectAdded += _vs_ProjectAdded;
            _vs.ProjectAddingStarted += _vs_ProjectAddingStarted;
            _vs.ProjectRemoved += _vs_ProjectRemoved;

            //hook type system
            _loader.AppDomain.MethodInvalidated += methodInvalidated;
            _loader.AppDomain.CompositionSchemeInvalidated += requireRedraw;
            _loader.AppDomain.ComponentAdded += (c) => requireRedraw();
            _loader.AppDomain.ComponentRemoved += (c) => requireRedraw();

            _vs.StartListening();
        }


        #endregion

        #region User extensions loading

        /// <summary>
        /// Load user extensions from extension assemblies.
        /// </summary>
        private void loadUserExtensions()
        {
            var exports = collectExports();

            //process all exports
            var exportedProviders = new List<ExportedAssemblyProviderFactory>();
            var drawingProviders = new List<ContentDrawer>();
            _connectorFactories = new ConnectorFactory[Enum.GetValues(typeof(ConnectorAlign)).Length];
            foreach (var export in exports)
            {
                registerExport(export);

                //collect exported providers
                exportedProviders.AddRange(export.ExportedProviders);
                drawingProviders.AddRange(createContentDrawers(export));

                if (export.ExportedGeneralDrawingDefinitionProvider != null)
                    _exportedGeneralDrawingProvider.Add(export.ExportedGeneralDrawingDefinitionProvider);

                foreach (var connectorProvider in export.ExportedConnectorFactories)
                    _connectorFactories[(int)connectorProvider.Key] = connectorProvider.Value;

                if (export.ExportedJoinFactory != null)
                    _joinFactory = export.ExportedJoinFactory;
            }

            //initialize editor
            var assembliesFactory = new ExportedProvidersFactory(exportedProviders);
            _loader = new AssemblyLoader(_settings, assembliesFactory);
            _contentDrawers = drawingProviders.ToArray();
        }

        /// <summary>
        /// Registers the export.
        /// </summary>
        /// <param name="export">The registered export.</param>
        private void registerExport(ExtensionExport export)
        {
            hookExportLogging(export);

            _vs.EditorLoadingExceptions(() => export.LoadExports(_settings.Runtime), "Registering exports");
        }

        /// <summary>
        /// Creates the content drawers defined by given export.
        /// </summary>
        /// <param name="export">The export that defines content drawers.</param>
        /// <returns>IEnumerable&lt;ContentDrawer&gt;.</returns>
        private IEnumerable<ContentDrawer> createContentDrawers(ExtensionExport export)
        {
            foreach (var exportedDrawer in export.ExportedDrawers)
            {
                yield return new ContentDrawer(exportedDrawer.Key, (i) => exportedDrawer.Value(i));
            }
        }

        /// <summary>
        /// Hooks logging of given export.
        /// </summary>
        /// <param name="export">The hooked export.</param>
        private void hookExportLogging(ExtensionExport export)
        {
            export.OnLog += logHandler;
        }


        /// <summary>
        /// Collects all exports.
        /// </summary>
        /// <returns>Collected exports.</returns>
        private IEnumerable<ExtensionExport> collectExports()
        {
            //MEF Composition of user extensions.
            var importer = new UserExtensionImporter(_vs);
            var directoryCatalog = new DirectoryCatalog(Installer.ExtensionPath);
            var container = new CompositionContainer(directoryCatalog);
            container.ComposeParts(importer);
            return importer.Exports;
        }

        #endregion

        #region Drawing providing routines

        /// <summary>
        /// Show composition based on analysis of given method.
        /// </summary>
        /// <param name="compositionPoint">Composition point to be analyzed.</param>
        private void showComposition(CompositionPoint compositionPoint)
        {
            _vs.SafeRunAction(() =>
            {
                if (_currentResult != null)
                {
                    //invalidate result, to free up resources
                    UserInteraction.DisposeResources();
                }

                if (compositionPoint == null)
                {
                    _guiManager.Display(null, true);
                    _currentResult = null;
                }
                else
                {
                    var watch = Stopwatch.StartNew();

                    runAnalysis(compositionPoint);
                    _vs.Log.Message("Executing composition point {0}ms", watch.ElapsedMilliseconds);

                    if (_analysisError == null)
                    {
                        watch.Restart();

                        //analysis has been successful
                        var drawing = createDrawings(_currentResult);
                        _guiManager.Display(drawing);
                        _vs.Log.Message("Drawing composition point {0}ms", watch.ElapsedMilliseconds);
                    }
                    else
                    {
                        _guiManager.DisplayEntry(_analysisError);
                    }
                }
            }, "Refreshing composition point");
        }

        /// <summary>
        /// Run analysis on given composition point.
        /// </summary>
        /// <param name="compositionPoint">Composition point to be analyzed.</param>
        private void runAnalysis(CompositionPoint compositionPoint)
        {
            _analysisError = null;

            try
            {
                var entryMethod = compositionPoint.EntryMethod;
                _loader.Settings.CodeBaseFullPath = getCodeBase(entryMethod);

                var entryArguments = getCompositionPointArguments(compositionPoint);
                _currentArguments = entryArguments;

                //run analysis on selected compsition with obtained arguments
                _currentResult = _machine.Run(_loader, entryMethod, entryArguments);
                _currentResult.OnViewCommit += (v) =>
                {
                    _vs.ForceFlushChanges();
                };

                handleRuntimeException(_currentResult.RuntimeException);
            }
            catch (Exception ex)
            {
                _currentArguments = null;
                _analysisError = _vs.LogErrorEntry(ex.Message, ex.ToString());
            }
        }

        /// <summary>
        /// Gets code base in context of given method.
        /// </summary>
        /// <param name="entryMethod">The context entry method.</param>
        /// <returns>Code base if available, empty <c>string</c> otherwise.</returns>
        private string getCodeBase(MethodID entryMethod)
        {
            var entryAssembly = _loader.AppDomain.GetDefiningAssemblyProvider(entryMethod);
            if (entryAssembly == null)
                return "";

            var codeBase = entryAssembly.FullPathMapping;
            if (codeBase == null)
                return "";

            return Path.GetDirectoryName(codeBase);
        }

        /// <summary>
        /// Handle exception that could be thrown at analysis runtime.
        /// </summary>
        /// <param name="exception">Runtime exception.</param>
        private void handleRuntimeException(Exception exception)
        {
            if (exception == null)
                return;

            var parsingException = exception as ParsingException;
            if (parsingException != null)
            {
                _analysisError = _vs.LogErrorEntry(parsingException.Message, parsingException.ToString(), parsingException.Navigate);
            }
            else
            {
                _analysisError = _vs.LogErrorEntry(exception.Message, exception.ToString());
            }
        }

        /// <summary>
        /// Get arguments for given composition point.
        /// </summary>
        /// <param name="compositionPoint">Composition point which arguments are requested.</param>
        /// <returns>Composition point's arguments.</returns>
        /// <exception cref="System.InvalidOperationException">Preparing composition point arguments failed</exception>
        private Instance[] getCompositionPointArguments(CompositionPoint compositionPoint)
        {
            var entryMethod = compositionPoint.EntryMethod;
            var entryArguments = new List<Instance>();

            //prepare composition point arguments
            entryArguments.Add(_machine.CreateInstance(compositionPoint.DeclaringComponent));
            if (compositionPoint.ArgumentProvider != null)
            {
                var result = _machine.Run(_loader, compositionPoint.ArgumentProvider);
                var context = result.EntryContext;

                for (var i = 0; i < Naming.GetMethodParamCount(entryMethod); ++i)
                {
                    var argVariable = "arg" + i;
                    var entryArgument = context.GetValue(new VariableName(argVariable));
                    entryArguments.Add(entryArgument);
                }

                if (result.RuntimeException != null)
                    throw new InvalidOperationException("Preparing composition point arguments failed", result.RuntimeException);
            }

            return entryArguments.ToArray();
        }

        /// <summary>
        /// Refresh drawing according to current composition point
        /// <remarks>Is called only</remarks>.
        /// </summary>
        private void refreshDrawing()
        {
            _guiManager.DispatchedAction(() =>
            {
                if (_guiManager.FlushCompositionPointUpdates())
                    //flushing will cause event that refresh drawing again - so 
                    //we don't need to refresh drawing immediately
                    return;

                _guiManager.DoEvents();
                var compositionPoint = _guiManager.SelectedCompositionPoint;
                showComposition(compositionPoint);
            });
        }

        /// <summary>
        /// Create drawings from given result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>DiagramDefinition.</returns>
        private DiagramDefinition createDrawings(AnalyzingResult result)
        {
            var pipeline = _loader.Settings.Runtime.CreateDrawingPipeline(generalDrawer, result);
            var entryInstance = _currentArguments == null || _currentArguments.Length == 0 ? null : _currentArguments[0];

            foreach (var instance in result.CreatedInstances)
            {
                var isEntryInstance = instance == entryInstance;
                var hasDrawer = Runtime.GetDrawer(instance) != null;
                var hasComponentInfo = _loader.GetComponentInfo(instance.Info) != null;

                var addToQueue = hasDrawer || hasComponentInfo;

                if (addToQueue)
                {
                    pipeline.AddToDrawQueue(instance);
                    if (hasComponentInfo)
                        pipeline.ForceDisplay(instance);
                }
            }

            var definition = pipeline.GetOutput();
            foreach (var instance in pipeline.DisplayedInstances)
            {
                if (instance.IsDirty)
                    _vs.Log.Warning("Instance {0} is marked as dirty, therefore it's display can be incorrect", instance.ID);
            }

            definition.AddEditsMenu("Add Component", componentCreationEdits);
            definition.AddCommand(new CommandDefinition("Reset workspace", () => _vs.SafeRunAction(_guiManager.ResetWorkspace, "Resetting workspace failed")));
            definition.UseItemAvoidance = _guiManager.UseItemAvoidance;
            definition.UseJoinAvoidance = _guiManager.UseJoinAvoidance;
            definition.ShowJoinLines = _guiManager.ShowJoinLines;

            return definition;
        }

        /// <summary>
        /// Creates edits for creating components.
        /// </summary>
        /// <returns>Edits for creating components.</returns>
        private IEnumerable<EditDefinition> componentCreationEdits()
        {
            var result = new List<EditDefinition>();

            if (_currentResult == null || _currentResult.EntryContext == null)
                return null;

            var call = _currentResult.EntryContext.EntryBlock.Call;

            var assembly = _loader.AppDomain.GetDefiningAssembly(call.Name);
            if (assembly == null)
                return result;

            var components = assembly.GetReferencedComponents();
            foreach (var component in components)
            {
                if (component.ImportingConstructor == null)
                    continue;

                if (!Naming.IsParamLessCtor(component.ImportingConstructor))
                    continue;

                var edit = new EditDefinition("Create " + component.ComponentType.TypeName, (v) =>
                {
                    return _vs.SafeRun(() =>
                    {
                        var transformation = new AddCallTransformation((exV) => addComponent(component, exV));

                        var view = (v as EditView).CopyView();
                        view.Apply(transformation);
                        return EditView.Wrap(view);
                    }, "Creation edit failed");
                }, (v) => true);

                result.Add(edit);
            }

            return result;

        }

        /// <summary>
        /// Add component into composition point.
        /// </summary>
        /// <param name="component">The component to add.</param>
        /// <param name="v">View of composition point.</param>
        /// <returns>Edit info for component adding.</returns>
        private CallEditInfo addComponent(ComponentInfo component, ExecutionView v)
        {
            var call = new CallEditInfo(component.ComponentType, Naming.CtorName);

            var name = TypeSystem.Dialogs.VariableName.GetName(component.ComponentType, _currentResult.EntryContext);
            if (name == null)
            {
                v.Abort("User aborted component adding");
                return null;
            }

            call.ReturnName = name;
            return call;
        }

        /// <summary>
        /// General drawing provider that is commonly used for all instances
        /// Drawings of required instances are specialized by concrete drawers.
        /// </summary>
        /// <param name="instance">Instance to be drawn.</param>
        private void generalDrawer(DrawedInstance instance)
        {
            if (instance.WrappedInstance.CreationNavigation != null)
                instance.Drawing.AddCommand(new CommandDefinition("Navigate to", () =>
                {
                    _vs.SafeRunAction(() => instance.WrappedInstance.CreationNavigation(),
                        "Instance navigation failed");
                }));

            instance.Drawing.AddEdit(new EditDefinition("Remove", (v) =>
            {
                return _vs.SafeRun(() =>
                 {
                     var view = (v as EditView).CopyView();
                     view.Remove(instance.WrappedInstance);
                     return EditView.Wrap(view);
                 }, "Removing edit failed");
            }, (v) =>
            {
                var view = (v as EditView).CopyView();
                return view.CanRemove(instance.WrappedInstance);
            }));

            var instanceInfo = instance.WrappedInstance.Info;
            var componentInfo = _loader.GetComponentInfo(instanceInfo);
            var assembly = _loader.AppDomain.GetDefiningAssembly(instanceInfo);
            if (assembly != null)
                instance.SetProperty("DefiningAssembly", assembly.Name);

            //provide general drawing
            foreach (var generalDrawer in _exportedGeneralDrawingProvider)
            {
                generalDrawer(instance, componentInfo);
            }
        }

        #endregion

        #region Transaction handling

        /// <summary>
        /// Attach refreshRedraw routine into root transaction.
        /// </summary>
        private void requireRedraw()
        {
            var action = new TransactionAction(refreshDrawing, "RefreshDrawing", (t) => t.Name == "RefreshDrawing", this);
            Transactions.AttachAfterAction(null, action);
        }

        #endregion

        #region Event handlers

        /// <summary>
        /// Handler for log events from TypeSystem.
        /// </summary>
        /// <param name="category">Category of logged message.</param>
        /// <param name="message">Logged message.</param>
        private void logHandler(string category, string message)
        {
            LogLevels level;
            switch (category)
            {
                case "ERROR":
                    level = LogLevels.Error;
                    break;
                case "MESSAGE":
                    level = LogLevels.Message;
                    break;
                case "WARNING":
                    level = LogLevels.Warning;
                    break;
                default:
                    level = LogLevels.Notification;
                    break;
            }

            var entry = new LogEntry(level, message, null, null);
            _vs.Log.Entry(entry);
        }

        /// <summary>
        /// Try to show progress of given <see cref="Transaction" />.
        /// </summary>
        /// <param name="transaction">Transaction which progress can be shown.</param>
        private void tryShowProgress(Transaction transaction)
        {
            if (!_showProgress)
                return;

            if (_progressWatch.ElapsedMilliseconds < 100)
                //prevent progressing too often
                return;

            _progressWatch.Restart();

            var message = transaction.Description;
            var progress = transaction.ProgressStatus;

            if (progress != null)
                message += " (" + progress + ")";

            _guiManager.DisplayLoadingMessage(message);
            _guiManager.DoEvents();
        }

        /// <summary>
        /// Handler called for methods that has been invalidated.
        /// </summary>
        /// <param name="invalidatedMethod">Identifier of invalidated method.</param>
        private void methodInvalidated(MethodID invalidatedMethod)
        {
            _vs.Log.Message("Method invalidation {0}", invalidatedMethod);

            if (!_guiManager.AutoRefresh)
                return;

            if (_currentResult == null)
                return;

            if (!_currentResult.Uses(invalidatedMethod))
                return;

            requireRedraw();
        }

        /// <summary>
        /// Handler called before project is added. Transaction for project adding is started.
        /// </summary>
        /// <param name="project">Project that will be added.</param>
        void _vs_ProjectAddingStarted(EnvDTE.Project project)
        {
            _projectAddTransaction = Transactions.StartNew("Loading project " + project.Name);
        }

        /// <summary>
        /// Handler called when project is added.
        /// </summary>
        /// <param name="project">Added project.</param>
        private void _vs_ProjectAdded(EnvDTE.Project project)
        {
            _loader.LoadRoot(project);

            _projectAddTransaction.Commit();
            _projectAddTransaction = null;
        }

        /// <summary>
        /// Handler called when project is removed.
        /// </summary>
        /// <param name="project">Removed project.</param>
        void _vs_ProjectRemoved(EnvDTE.Project project)
        {
            var tr = Transactions.StartNew("Removing project");
            _loader.Unload(project);
            tr.Commit();
        }

        /// <summary>
        /// Handler called before solution is opened. Here opening solution transaction
        /// is started.
        /// </summary>
        void _vs_SolutionOpeningStarted()
        {
            setProgressVisibility(true);
            _solutionOpenTransaction = Transactions.StartNew("Opening solution");
        }

        /// <summary>
        /// Handler called when solution is opened.
        /// </summary>
        private void _vs_SolutionOpened()
        {
            _solutionOpenTransaction.Commit();
            _solutionOpenTransaction = null;

            setProgressVisibility(false);
        }

        /// <summary>
        /// Handler called when solution is closed.
        /// </summary>
        private void _vs_SolutionClosed()
        {
            //REA will be also freed
            _guiManager.DisableHostedApplication();
            //unload assemblies because of disposing resources
            _loader.UnloadAssemblies();
        }

        /// <summary>
        /// Set visibility of progress informing.
        /// </summary>
        /// <param name="isShown">Determine that progress will be shown or not.</param>
        private void setProgressVisibility(bool isShown)
        {
            if (isShown)
            {
                _progressWatch.Start();
                _showProgress = true;
                _guiManager.EnableDoEvents = true;
            }
            else
            {
                _progressWatch.Stop();
                _showProgress = false;
                _guiManager.EnableDoEvents = false;
                _guiManager.ShowWorkspace();
            }
        }

        #endregion
    }
}
