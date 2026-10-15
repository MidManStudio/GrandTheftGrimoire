// Stand-ins for the types other packages of this project provide. Verification harness only.
using System;

namespace MidManStudio.Mdix.Unity
{
    /// <summary>Marks a ScriptableObject that the MDIX importer can bake a file into.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MdixBakeableAttribute : Attribute
    {
        public MdixBakeableAttribute(string a, string b) { }
    }
}
