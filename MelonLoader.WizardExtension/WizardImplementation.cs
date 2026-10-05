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

            while (true)
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    throw new WizardBackoutException();

                try
                {
                    GameInspector inspector = new();
                    GameInfo game = inspector.Inspect(dialog.FileName);
                    ProjectGenerator generator = new();
                    foreach (KeyValuePair<string, string> replacement in generator.CreateReplacements(game, Environment.UserName))
                        replacementsDictionary.Add(replacement.Key, replacement.Value);
                    return;
                }
                catch (Exception exception)
                {
                    if (MessageBox.Show(exception.Message, "Error", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
                        throw new WizardBackoutException();
                }
            }
        }

        public void BeforeOpeningFile(ProjectItem projectItem) { }
        public void ProjectFinishedGenerating(Project project) { }
        public void RunFinished() { }
        public bool ShouldAddProjectItem(string filePath) => true;
        public void ProjectItemFinishedGenerating(ProjectItem projectItem) { }
    }
}
