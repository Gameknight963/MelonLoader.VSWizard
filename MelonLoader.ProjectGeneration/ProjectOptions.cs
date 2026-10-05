namespace MelonLoader.ProjectGeneration
{
    public enum ProjectKind { Mod, Plugin }

    public sealed class ProjectOptions
    {
        public string ProjectName { get; set; }
        public string RootNamespace { get; set; }
        public string Author { get; set; }
        public ProjectKind Kind { get; set; }
        public GameInfo Game { get; set; }
    }
}
