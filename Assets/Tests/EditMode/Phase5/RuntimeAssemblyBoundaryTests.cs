using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class RuntimeAssemblyBoundaryTests
    {
        [Test]
        public void RuntimeAssembly_ReferencesUguiAndTextMeshProWithoutUnityEditor()
        {
            var runtimeAssembly = CompilationPipeline.GetAssemblies()
                .Single(assembly => assembly.name == "AnimalCafe.Runtime");
            var references = System.Reflection.Assembly.Load(File.ReadAllBytes(runtimeAssembly.outputPath))
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Contain("UnityEngine.UI"));
            Assert.That(references, Does.Contain("Unity.TextMeshPro"));
            Assert.That(references.Any(reference => reference.StartsWith("UnityEditor")), Is.False);
        }
    }
}
