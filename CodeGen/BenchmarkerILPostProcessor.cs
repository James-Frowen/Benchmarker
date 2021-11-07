using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JamesFrowen.Benchmarker.Weaver;
using Mirage.Weaver;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace JamesFrowen.Benchmarker.ILWeave
{
    public class BenchmarkerILPostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            return compiledAssembly.References.Any(filePath => Path.GetFileNameWithoutExtension(filePath) == "JamesFrowen.Benchmarker");
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            try
            {
                AssemblyDefinition assembly = Mirage.Weaver.Weaver.AssemblyDefinitionFor(compiledAssembly);
                ModuleDefinition module = assembly.MainModule;
                ProcessAllMethods(module);

                // write
                var pe = new MemoryStream();
                var pdb = new MemoryStream();

                var writerParameters = new WriterParameters
                {
                    SymbolWriterProvider = new PortablePdbWriterProvider(),
                    SymbolStream = pdb,
                    WriteSymbols = true
                };

                assembly?.Write(pe, writerParameters);

                return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), new List<Unity.CompilationPipeline.Common.Diagnostics.DiagnosticMessage>());
            }
            catch (Exception e)
            {
                return new ILPostProcessResult(null, new List<Unity.CompilationPipeline.Common.Diagnostics.DiagnosticMessage> {
                    new Unity.CompilationPipeline.Common.Diagnostics.DiagnosticMessage{
                        DiagnosticType=  Unity.CompilationPipeline.Common.Diagnostics.DiagnosticType.Error,
                        MessageData = $"Failed to weaver Benchmarker: {e}",
                    } });
            }
        }

        void ProcessAllMethods(ModuleDefinition module)
        {
            bool any = false;
            // create copies of collections incase we add any
            foreach (TypeDefinition type in module.Types.ToArray())
            {
                foreach (MethodDefinition method in type.Methods.ToArray())
                {
                    if (method.HasCustomAttribute<BenchmarkMethodAttribute>())
                    {
                        InsertBenchmark(module, method);
                        any = true;
                    }
                }
            }

            if (any)
            {
                ILProcessor worker = GeneratedMethod(module);
                worker.Append(worker.Create(OpCodes.Ret));
            }
        }

        void InsertBenchmark(ModuleDefinition module, MethodDefinition method)
        {
            VariableDefinition timeVar = method.AddLocal<long>();

            ILProcessor worker = method.Body.GetILProcessor();
            Instruction first = method.Body.Instructions.First();
            worker.InsertBefore(first, worker.Create(OpCodes.Call, () => BenchmarkHelper.GetTimestamp()));
            worker.InsertBefore(first, worker.Create(OpCodes.Stloc, timeVar));

            string name = method.FullName;

            // for each return, add BenchmarkHelper.EndMethod
            foreach (Instruction oldRet in method.Body.Instructions.Where(x => x.OpCode == OpCodes.Ret).ToArray())
            {
                // change to nop, just incase jumps go to this return
                oldRet.OpCode = OpCodes.Nop;

                // add new ret after old one, and then insert BenchmarkHelper.EndMethod after
                Instruction newRet = worker.Create(OpCodes.Ret);
                worker.InsertAfter(oldRet, newRet);
                worker.InsertBefore(newRet, worker.Create(OpCodes.Ldc_I4, name.GetHashCode()));
                worker.InsertBefore(newRet, worker.Create(OpCodes.Ldloc, timeVar));
                worker.InsertBefore(newRet, worker.Create(OpCodes.Call, () => BenchmarkHelper.EndMethod(default, default)));
            }

            RegisterMethod(module, name);
        }

        public static TypeDefinition GeneratedClass(ModuleDefinition module)
        {
            TypeDefinition type = module.GetType("JamesFrowen.Benchmarker", "BenchmarkInit");
            if (type != null)
                return type;

            type = new TypeDefinition("JamesFrowen.Benchmarker", "BenchmarkInit",
                        TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Abstract | TypeAttributes.Sealed,
                        module.ImportReference<object>());
            module.Types.Add(type);
            return type;
        }

        public static ILProcessor GeneratedMethod(ModuleDefinition module)
        {
            TypeDefinition generatedClass = GeneratedClass(module);
            MethodDefinition method = generatedClass.GetMethod("InitMethods");
            if (method != null)
            {
                return method.Body.GetILProcessor();
            }
            else
            {
                method = generatedClass.AddMethod(
                   "InitMethods",
                   Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static);

                ConstructorInfo attributeconstructor = typeof(RuntimeInitializeOnLoadMethodAttribute).GetConstructor(new[] { typeof(RuntimeInitializeLoadType) });

                var customAttributeRef = new CustomAttribute(module.ImportReference(attributeconstructor));
                customAttributeRef.ConstructorArguments.Add(new CustomAttributeArgument(module.ImportReference<RuntimeInitializeLoadType>(), RuntimeInitializeLoadType.BeforeSceneLoad));
                method.CustomAttributes.Add(customAttributeRef);

                ILProcessor worker = method.Body.GetILProcessor();

                return worker;
            }
        }

        public static void RegisterMethod(ModuleDefinition module, string methodName)
        {
            ILProcessor worker = GeneratedMethod(module);

            worker.Append(worker.Create(OpCodes.Ldstr, methodName));
            worker.Append(worker.Create(OpCodes.Call, () => BenchmarkHelper.RegisterMethod(default)));
        }
    }
}