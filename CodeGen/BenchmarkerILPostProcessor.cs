/*
MIT License

Copyright (c) 2022 James Frowen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Linq;
using JamesFrowen.Benchmarker.Weaver;
using Mirage.CodeGen;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace JamesFrowen.Benchmarker.ILWeave
{
    public class BenchmarkerILPostProcessor : ILPostProcessor
    {
        public const string RuntimeAssemblyName = "JamesFrowen.Benchmarker";

        public sealed override ILPostProcessor GetInstance() => this;

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            return ILPPHelper.CreateAndProcess(compiledAssembly, RuntimeAssemblyName, _ => new BenchmarkerWeaver());
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly) => ILPPHelper.WillProcess(compiledAssembly, RuntimeAssemblyName);
    }

    public class BenchmarkerWeaver : WeaverBase
    {
        protected override ResultType Process(AssemblyDefinition assembly, ICompiledAssembly compiledAssembly)
        {
            var module = assembly.MainModule;
            var anyChanges = ProcessAllMethods(module);

            return anyChanges
                ? ResultType.Success
                : ResultType.NoChanges;
        }

        private bool ProcessAllMethods(ModuleDefinition module)
        {
            var any = false;
            // create copies of collections incase we add any
            foreach (var type in module.Types.ToArray())
            {
                foreach (var method in type.Methods.ToArray())
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
                var worker = GeneratedMethod(module);
                worker.Append(worker.Create(OpCodes.Ret));
            }
            return any;
        }

        private void InsertBenchmark(ModuleDefinition module, MethodDefinition method)
        {
            var timeVar = method.AddLocal<long>();

            var worker = method.Body.GetILProcessor();
            var first = method.Body.Instructions.First();
            worker.InsertBefore(first, worker.Create(OpCodes.Call, () => BenchmarkHelper.GetTimestamp()));
            worker.InsertBefore(first, worker.Create(OpCodes.Stloc, timeVar));

            var name = method.FullName;

            // for each return, add BenchmarkHelper.EndMethod
            foreach (var oldRet in method.Body.Instructions.Where(x => x.OpCode == OpCodes.Ret).ToArray())
            {
                // change to nop, just incase jumps go to this return
                oldRet.OpCode = OpCodes.Nop;

                // add new ret after old one, and then insert BenchmarkHelper.EndMethod after
                var newRet = worker.Create(OpCodes.Ret);
                worker.InsertAfter(oldRet, newRet);
                worker.InsertBefore(newRet, worker.Create(OpCodes.Ldc_I4, name.GetHashCode()));
                worker.InsertBefore(newRet, worker.Create(OpCodes.Ldloc, timeVar));
                worker.InsertBefore(newRet, worker.Create(OpCodes.Call, () => BenchmarkHelper.EndMethod(default, default)));
            }

            RegisterMethod(module, name);
        }

        public static TypeDefinition GeneratedClass(ModuleDefinition module)
        {
            var type = module.GetType("JamesFrowen.Benchmarker", "BenchmarkInit");
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
            var generatedClass = GeneratedClass(module);
            var method = generatedClass.GetMethod("InitMethods");
            if (method != null)
            {
                return method.Body.GetILProcessor();
            }
            else
            {
                method = generatedClass.AddMethod(
                   "InitMethods",
                   Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static);

                var attributeconstructor = typeof(RuntimeInitializeOnLoadMethodAttribute).GetConstructor(new[] { typeof(RuntimeInitializeLoadType) });

                var customAttributeRef = new CustomAttribute(module.ImportReference(attributeconstructor));
                customAttributeRef.ConstructorArguments.Add(new CustomAttributeArgument(module.ImportReference<RuntimeInitializeLoadType>(), RuntimeInitializeLoadType.BeforeSceneLoad));
                method.CustomAttributes.Add(customAttributeRef);

                var worker = method.Body.GetILProcessor();

                return worker;
            }
        }

        public static void RegisterMethod(ModuleDefinition module, string methodName)
        {
            var worker = GeneratedMethod(module);

            worker.Append(worker.Create(OpCodes.Ldstr, methodName));
            worker.Append(worker.Create(OpCodes.Call, () => BenchmarkHelper.RegisterMethod(default)));
        }
    }
}
