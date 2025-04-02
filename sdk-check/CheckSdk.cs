using System;
using System.IO;
using System.Reflection;
using System.Linq;

class CheckSdk
{
    static void Main()
    {
        try
        {
            // Load the SDK assembly
            Assembly assembly = Assembly.LoadFrom("Playnite.SDK.dll");
            
            // Find the IDialogs interface
            Type dialogsType = assembly.GetTypes().FirstOrDefault(t => t.Name == "IDialogs");
            
            if (dialogsType == null)
            {
                Console.WriteLine("IDialogs interface not found!");
                return;
            }
            
            Console.WriteLine("Found IDialogs interface. Methods:");
            
            // Display SelectFile method signatures
            var selectFileMethods = dialogsType.GetMethods().Where(m => m.Name == "SelectFile");
            foreach (var method in selectFileMethods)
            {
                Console.WriteLine($"  {method.ReturnType.Name} SelectFile({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
            }
            
            // Display SelectFolder method signatures
            var selectFolderMethods = dialogsType.GetMethods().Where(m => m.Name == "SelectFolder");
            foreach (var method in selectFolderMethods)
            {
                Console.WriteLine($"  {method.ReturnType.Name} SelectFolder({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
            }
            
            // Display SelectString method signatures
            var selectStringMethods = dialogsType.GetMethods().Where(m => m.Name == "SelectString");
            foreach (var method in selectStringMethods)
            {
                Console.WriteLine($"  {method.ReturnType.Name} SelectString({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
            }
            
            // Find StringSelectionDialogResult type
            Type stringSelectionResultType = assembly.GetTypes().FirstOrDefault(t => t.Name == "StringSelectionDialogResult");
            if (stringSelectionResultType != null)
            {
                Console.WriteLine("\nStringSelectionDialogResult properties:");
                foreach (var prop in stringSelectionResultType.GetProperties())
                {
                    Console.WriteLine($"  {prop.PropertyType.Name} {prop.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}