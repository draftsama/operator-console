#nullable enable

using System;
using UnityEngine.Scripting;

namespace OperatorConsole
{
    /// <summary>
    /// Marks a method as a Command shown on the Runtime Panel once its owner calls <see cref="OCCommandRegistry.Register(object)"/>.
    /// The method takes no parameters or a single <see cref="System.Threading.CancellationToken"/>, and returns void or UniTask.
    /// </summary>
    /// <remarks>Derives from <see cref="PreserveAttribute"/> because the method is reached only through reflection, which IL2CPP code stripping can't see.</remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class OCCommandAttribute : PreserveAttribute
    {
        public OCCommandAttribute(string title) => Title = title;

        public string Title { get; }
        public string? Description { get; set; }
        public bool Confirm { get; set; }

        /// <summary>Tag names, as declared in the Config. None: every Runtime Panel shows the Command.</summary>
        public string[]? Tags { get; set; }

        public string? Header { get; set; }
        public int Order { get; set; }
    }
}
