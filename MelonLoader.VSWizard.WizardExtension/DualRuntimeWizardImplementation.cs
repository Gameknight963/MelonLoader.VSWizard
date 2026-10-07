using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;
using MelonLoader.VSWizard.ProjectGeneration;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace MelonLoader.VSWizard.WizardExtension
{
    public sealed class DualRuntimeWizardImplementation : IWizard
    {
        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            try
            {
                GameInfo mono = SelectGame(false);
                GameInfo il2Cpp = SelectGame(true);
                TemplateRenderer renderer = new();
                Dictionary<string, string> replacements = renderer.CreateDualRuntimeReplacements(
                    new RuntimeTargetOptions { Game = mono }, new RuntimeTargetOptions { Game = il2Cpp }, Environment.UserName,
                    projectName: replacementsDictionary["$projectname$"], rootNamespace: replacementsDictionary["$safeprojectname$"]);
                foreach (KeyValuePair<string, string> replacement in replacements)
                    replacementsDictionary.Add(replacement.Key, replacement.Value);
            }
            catch (WizardBackoutException)
            {
                throw;
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new WizardBackoutException();
            }
        }

        private static GameInfo SelectGame(bool il2Cpp)
        {
            using OpenFileDialog dialog = new()
            {
                Title = il2Cpp ? "Select IL2CPP Game Executable" : "Select Mono Game Executable",
                Multiselect = false,
                Filter = "Unity Executables (*.exe)|*.exe"
            };
            if (dialog.ShowDialog() != DialogResult.OK)
                throw new WizardBackoutException();

            GameInspector inspector = new();
            GameInfo game = inspector.Inspect(dialog.FileName);
            if (game.IsIl2Cpp != il2Cpp)
                throw new InvalidOperationException("Select a " + (il2Cpp ? "IL2CPP" : "Mono") + " installation for this target.");
            // Validate this target before asking for the next installation.
            TemplateRenderer renderer = new();
            renderer.CreateReplacements(game, Environment.UserName);
            return game;
        }

        public void ProjectFinishedGenerating(Project project)
        {
            // Preserve an existing solution; add runtime configurations and map this project's contexts.
            SolutionConfigurations configurations = project.DTE.Solution.SolutionBuild.SolutionConfigurations;
            string[] configurationNames = { "Mono-Debug", "Mono-Release", "Il2Cpp-Debug", "Il2Cpp-Release" };
            foreach (string name in configurationNames)
            {
                bool exists = false;
                foreach (SolutionConfiguration configuration in configurations)
                    if (configuration.Name == name) exists = true;
                if (!exists) configurations.Add(name, "", false);
            }
            foreach (SolutionConfiguration configuration in configurations)
            {
                if (Array.IndexOf(configurationNames, configuration.Name) < 0) continue;
                foreach (SolutionContext context in configuration.SolutionContexts)
                {
                    if (string.Equals(context.ProjectName, project.UniqueName, StringComparison.OrdinalIgnoreCase))
                        context.ConfigurationName = configuration.Name + "|Any CPU";
                }
                if (configuration.Name == "Mono-Debug") configuration.Activate();
            }
        }

        public void BeforeOpeningFile(ProjectItem projectItem) { }
        public void RunFinished() { }
        public bool ShouldAddProjectItem(string filePath) => true;
        public void ProjectItemFinishedGenerating(ProjectItem projectItem) { }
    }
}
