using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;
using MelonLoader.ProjectGeneration;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace MelonLoader.WizardExtension
{
    public class WizardImplementation : IWizard
    {
        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            using OpenFileDialog dialog = new()
            {
                Title = "Select Game Executable",
                Multiselect = false,
                Filter = "Unity Executables (*.exe)|*.exe"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                throw new WizardBackoutException();

            try
            {
                GameInspector inspector = new();
                GameInfo game = inspector.Inspect(dialog.FileName);
                TemplateRenderer renderer = new();
                foreach (KeyValuePair<string, string> replacement in renderer.CreateReplacements(game, Environment.UserName))
                    replacementsDictionary.Add(replacement.Key, replacement.Value);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new WizardBackoutException();
            }
        }

        public void BeforeOpeningFile(ProjectItem projectItem) { }
        public void ProjectFinishedGenerating(Project project) { }
        public void RunFinished() { }
        public bool ShouldAddProjectItem(string filePath) => true;
        public void ProjectItemFinishedGenerating(ProjectItem projectItem) { }
    }
}
