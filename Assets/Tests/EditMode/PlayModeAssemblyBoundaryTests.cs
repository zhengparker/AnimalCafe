using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace AnimalCafe.Tests.EditMode
{
    public sealed class PlayModeAssemblyBoundaryTests
    {
        [Test]
        public void PlayModeTestAssembly_DoesNotReferenceUnityEditor()
        {
            var playModeAssembly = CompilationPipeline.GetAssemblies()
                .Single(assembly => assembly.name == "AnimalCafe.PlayModeTests");
            var compiledAssembly = System.Reflection.Assembly.Load(
                File.ReadAllBytes(playModeAssembly.outputPath));
            var editorReferences = compiledAssembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name.StartsWith("UnityEditor"))
                .ToArray();

            Assert.That(editorReferences, Is.Empty,
                "Player-compatible PlayMode tests must not directly reference UnityEditor assemblies.");
        }
    }
}
