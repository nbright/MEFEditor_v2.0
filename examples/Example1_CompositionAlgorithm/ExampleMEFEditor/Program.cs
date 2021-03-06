﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ExtensionPoints;
using Example_extensions_referenced;

using MEFEditor;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;


namespace Main
{

    class Program
    {
        [Import]
        ILayout compositionResult = null;

        [ImportMany]
        ILogger[] loggers = null;

        [CompositionPoint]
        void Compose()
        { 
            var directoryCatalog = new DirectoryCatalog("Extensions");
            var wrongLayout = new WrongLayout();
            var consoleLogger = new ConsoleLogger();
            var aggregateCatalog = new AggregateCatalog();
            var typeCatalog = new TypeCatalog(typeof(SimpleLayout));
            aggregateCatalog.Catalogs.Add(typeCatalog);
            aggregateCatalog.Catalogs.Add(directoryCatalog);
            var compositionContainer = new CompositionContainer();
            compositionContainer.ComposeParts(this, wrongLayout);
        }


        static void Main(string[] args)
        {
            var prg = new Program();
            prg.Compose();

            string outputHTML = prg.compositionResult == null ? "Composition wasn't completed" : prg.compositionResult.GetPageHTML;

            if (prg.loggers != null)
                foreach (var logger in prg.loggers)
                    logger.Log("Output HTML: " + outputHTML);

            var server = new HTTPServer(outputHTML);
        }
    }
}
