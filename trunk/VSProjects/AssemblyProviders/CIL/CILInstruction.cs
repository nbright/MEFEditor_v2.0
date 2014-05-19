﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;
using E = System.Reflection.Emit;

using Mono.Cecil;
using Mono.Cecil.Cil;

using Analyzing;
using TypeSystem;
using TypeSystem.TypeParsing;

using AssemblyProviders.CIL.ILAnalyzer;

namespace AssemblyProviders.CIL
{
    /// <summary>
    /// Describe CIL instruction with sufficient information for transcription into IAL.
    /// </summary>
    public class CILInstruction
    {
        /// <summary>
        /// All opcodes indexed according to instruction names.
        /// </summary>
        private static readonly Dictionary<string, OpCode> OpCodesTable = new Dictionary<string, OpCode>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Starting address of instruction
        /// </summary>
        public readonly int Address;

        /// <summary>
        /// OpCode of current instruction.
        /// </summary>
        public readonly OpCode OpCode;

        #region Instruction operands

        /// <summary>
        /// Data stored in instruction operand.
        /// </summary>
        public readonly object Data;

        /// <summary>
        /// Target address of branch instructions operand.
        /// </summary>
        public readonly int BranchAddressOperand = -1;

        /// <summary>
        /// If Data contains method information, the information is converted into TypeSystem representation.
        /// </summary>
        public readonly TypeMethodInfo MethodOperand;

        /// <summary>
        /// Setter of field info operand, if available.
        /// </summary>
        public readonly TypeMethodInfo SetterOperand;

        /// <summary>
        /// Getter of field info operand, if available.
        /// </summary>
        public readonly TypeMethodInfo GetterOperand;

        #endregion

        /// <summary>
        /// Initialize table of operands.
        /// </summary>
        static CILInstruction()
        {
            foreach (var opCodeField in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                var opCode = (OpCode)opCodeField.GetValue(null);
                OpCodesTable.Add(opCode.Name, opCode); ;
            }
        }

        /// <summary>
        /// Create CILInstruction from runtime .NET representation of instruction.
        /// </summary>
        /// <param name="instruction">Runtime .NET representation of instruction.</param>
        internal CILInstruction(ILInstruction instruction, TranscriptionContext context)
        {
            Address = instruction.Address;
            Data = instruction.Data;

            OpCode = OpCodesTable[instruction.OpCode.Name];

            //TODO resolve ctors
            MethodOperand = createMethodInfo(Data as MethodInfo);
            BranchAddressOperand = getBranchOffset(instruction);
            SetterOperand = createSetter(Data as FieldInfo);
            GetterOperand = createGetter(Data as FieldInfo);
        }

        /// <summary>
        /// Create CILInstruction from Mono.Cecil instruction representation of instructino.
        /// </summary>
        /// <param name="instruction">Mono.Cecil representation of instruction</param>
        internal CILInstruction(Instruction instruction, TranscriptionContext context)
        {
            Address = instruction.Offset;
            Data = instruction.Operand;

            OpCode = instruction.OpCode;
            MethodOperand = CreateMethodInfo(Data as MethodReference, needsDynamicResolving(OpCode), context);
            BranchAddressOperand = getBranchOffset(Data as Instruction);

            SetterOperand = createSetter(Data as FieldReference);
            GetterOperand = createGetter(Data as FieldReference);
        }

        /// <summary>
        /// TODO this should been refactored out into some helper class
        /// </summary>
        /// <param name="method"></param>
        /// <param name="needsDynamicResolution"></param>
        /// <returns></returns>
        internal static TypeMethodInfo CreateMethodInfo(MethodReference method, bool needsDynamicResolution, TranscriptionContext context)
        {
            if (method == null)
                return null;

            //Get available type helper
            var typeHelper = context == null ? new TypeReferenceHelper() : context.TypeHelper;

            var builder = new MethodInfoBuilder(method, typeHelper);
            builder.NeedsDynamicResolving = needsDynamicResolution;

            return builder.Build();
        }

        /// <summary>
        /// Create human readable description of instruction.
        /// </summary>
        /// <returns>Created description.</returns>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendFormat("0x{0:x4} {1,-10}", this.Address, this.OpCode.Name);

            if (this.Data != null)
            {
                if (this.Data is string)
                {
                    builder.Append("\"" + this.Data + "\"");
                }
                else
                {
                    builder.Append(this.Data.ToString());
                }
            }

            return builder.ToString();
        }


        #region Reflection instruction processing

        /// <summary>
        /// Create getter info for given field
        /// </summary>
        /// <param name="field">Field which getter is needed</param>
        /// <returns>Created getter</returns>
        private TypeMethodInfo createGetter(FieldInfo field)
        {
            if (field == null)
                return null;

            var name = Naming.GetterPrefix + field.Name;
            var declaringType = TypeDescriptor.Create(field.DeclaringType);
            var fieldType = TypeDescriptor.Create(field.FieldType);
            var isStatic = field.IsStatic;

            return new TypeMethodInfo(declaringType,
                name, fieldType, ParameterTypeInfo.NoParams,
                isStatic, TypeDescriptor.NoDescriptors);
        }

        /// <summary>
        /// Create setter info for given field
        /// </summary>
        /// <param name="field">Field which setter is needed</param>
        /// <returns>Created setter</returns>
        private TypeMethodInfo createSetter(FieldInfo field)
        {
            if (field == null)
                return null;

            var name = Naming.SetterPrefix + field.Name;
            var declaringType = TypeDescriptor.Create(field.DeclaringType);
            var fieldType = TypeDescriptor.Create(field.FieldType);
            var isStatic = field.IsStatic;

            return new TypeMethodInfo(declaringType,
                name, TypeDescriptor.Void, new ParameterTypeInfo[]{
                    ParameterTypeInfo.Create("value",fieldType)
                },
                isStatic, TypeDescriptor.NoDescriptors);
        }

        /// <summary>
        /// Get offset of branch target according to instruction
        /// </summary>
        /// <returns>Target offset</returns>
        private int getBranchOffset(ILInstruction instruction)
        {
            switch (instruction.OpCode.OperandType)
            {
                case E.OperandType.ShortInlineBrTarget:
                    return instruction.Address + instruction.Length + (int)Data;
                case E.OperandType.InlineBrTarget:
                    throw new NotImplementedException();
                default:
                    return -1;
            }
        }

        /// <summary>
        /// Create TypeMethodInfo from given methodInfo
        /// </summary>
        /// <param name="methodInfo">method which TypeMethodInfo is created</param>
        /// <returns>Created TypeMethodInfo</returns>
        private TypeMethodInfo createMethodInfo(MethodInfo methodInfo)
        {
            if (methodInfo == null)
                return null;

            return TypeMethodInfo.Create(methodInfo);
        }

        #endregion


        #region Mono Cecil instruction processing

        /// <summary>
        /// Determine that instruction with given opcode needs dynamic method resolving
        /// </summary>
        /// <param name="opcode">Opcode of instruction</param>
        /// <returns>True if dynamic resolving is needed, false otherwise</returns>
        private bool needsDynamicResolving(OpCode opcode)
        {
            switch (opcode.Name)
            {
                case "callvirt":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Create getter info for given field
        /// </summary>
        /// <param name="field">Field which getter is needed</param>
        /// <returns>Created getter</returns>
        private TypeMethodInfo createGetter(FieldReference field)
        {
            if (field == null)
                return null;

            var name = Naming.GetterPrefix + field.Name;
            var declaringType = getInfo(field.DeclaringType);
            var fieldType = getInfo(field.FieldType);

            //TODO resolve if it is static
            return new TypeMethodInfo(declaringType,
                name, fieldType, ParameterTypeInfo.NoParams,
                true, TypeDescriptor.NoDescriptors);
        }

        /// <summary>
        /// Create setter info for given field
        /// </summary>
        /// <param name="field">Field which setter is needed</param>
        /// <returns>Created setter</returns>
        private TypeMethodInfo createSetter(FieldReference field)
        {
            if (field == null)
                return null;

            var name = Naming.SetterPrefix + field.Name;
            var declaringType = getInfo(field.DeclaringType);
            var fieldType = getInfo(field.FieldType);

            //TODO resolve if it is static
            return new TypeMethodInfo(declaringType,
                name, TypeDescriptor.Void, new ParameterTypeInfo[]{
                    ParameterTypeInfo.Create("value",fieldType)
                },
                true, TypeDescriptor.NoDescriptors);
        }

        /// <summary>
        /// Get offset of branch target according to instruction
        /// </summary>
        /// <returns>Target offset</returns>
        private int getBranchOffset(Instruction instruction)
        {
            if (instruction == null)
                return -1;

            return instruction.Offset;
        }

        /// <summary>
        /// TODO this should be refactored out into some helper class
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static TypeDescriptor getInfo(TypeReference type)
        {
            var builder = new TypeReferenceHelper();
            return builder.BuildDescriptor(type);
        }
        #endregion
    }
}
