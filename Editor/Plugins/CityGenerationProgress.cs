using System;
using UnityEditor;
using UnityEngine;

namespace BSCCityBuilder.Editor.Plugins
{
/// <summary>
/// Finestra di progresso condivisa dalle operazioni di generazione sincrone.
/// Supporta chiamate annidate e garantisce la chiusura anche in caso di eccezione.
/// </summary>
public static class CityGenerationProgress
{
    private static int operationDepth;
    private static string operationTitle = "Generazione città";

    public static T Run<T>(string title, string initialStatus, Func<T> operation)
    {
        bool isRoot = operationDepth == 0;
        if (isRoot)
        {
            operationTitle = string.IsNullOrWhiteSpace(title) ? "Generazione città" : title;
        }

        operationDepth++;
        try
        {
            Report(0f, initialStatus);
            T result = operation();
            Report(1f, "Completamento...");
            return result;
        }
        finally
        {
            operationDepth = Mathf.Max(0, operationDepth - 1);
            if (isRoot)
            {
                EditorUtility.ClearProgressBar();
                operationTitle = "Generazione città";
            }
        }
    }

    public static void Report(float progress, string status)
    {
        if (operationDepth <= 0) return;
        EditorUtility.DisplayProgressBar(
            operationTitle,
            string.IsNullOrWhiteSpace(status) ? "Elaborazione..." : status,
            Mathf.Clamp01(progress));
    }
}
}
