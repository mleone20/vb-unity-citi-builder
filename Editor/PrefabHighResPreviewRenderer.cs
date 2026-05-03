using UnityEditor;
using UnityEngine;

public static class PrefabHighResPreviewRenderer
{
    public static bool TryRenderToPng(GameObject prefabAsset, int width, int height, out Texture2D previewTexture, out byte[] pngBytes, out string error)
    {
        previewTexture = null;
        pngBytes = null;
        error = null;

        if (prefabAsset == null)
        {
            error = "Prefab nullo.";
            return false;
        }

        width = Mathf.Clamp(width, 128, 2048);
        height = Mathf.Clamp(height, 128, 2048);

        PreviewRenderUtility previewUtility = null;
        GameObject instance = null;

        try
        {
            previewUtility = new PreviewRenderUtility();
            previewUtility.cameraFieldOfView = 30f;
            previewUtility.lights[0].intensity = 1.15f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            previewUtility.lights[1].intensity = 1.05f;
            previewUtility.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 177f);
            previewUtility.ambientColor = new Color(0.35f, 0.35f, 0.35f, 1f);

            instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(prefabAsset);
            }

            if (instance == null)
            {
                error = "Impossibile istanziare il prefab per il rendering preview.";
                return false;
            }

            instance.hideFlags = HideFlags.HideAndDontSave;
            previewUtility.AddSingleGO(instance);

            Bounds bounds = ComputeInstanceBounds(instance);
            SetupCamera(previewUtility, bounds);

            Rect renderRect = new Rect(0f, 0f, width, height);
            previewUtility.BeginPreview(renderRect, GUIStyle.none);
            previewUtility.camera.Render();
            Texture texture = previewUtility.EndPreview();

            if (texture == null)
            {
                error = "Rendering preview fallito: texture non disponibile.";
                return false;
            }

            previewTexture = TextureToReadableTexture(texture, width, height);
            if (previewTexture == null)
            {
                error = "Rendering preview fallito: conversione texture non riuscita.";
                return false;
            }

            pngBytes = previewTexture.EncodeToPNG();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                error = "Rendering preview fallito: PNG vuoto.";
                return false;
            }

            return true;
        }
        catch (System.Exception ex)
        {
            error = "Errore durante il rendering preview high-res: " + ex.Message;
            return false;
        }
        finally
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }

            if (previewUtility != null)
            {
                previewUtility.Cleanup();
            }
        }
    }

    private static Bounds ComputeInstanceBounds(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one * 2f);
        }

        bool initialized = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!initialized)
            {
                result = renderer.bounds;
                initialized = true;
            }
            else
            {
                result.Encapsulate(renderer.bounds);
            }
        }

        return initialized ? result : new Bounds(Vector3.zero, Vector3.one * 2f);
    }

    private static void SetupCamera(PreviewRenderUtility previewUtility, Bounds bounds)
    {
        Vector3 center = bounds.center;
        float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
        float fovRad = previewUtility.camera.fieldOfView * Mathf.Deg2Rad;
        float distance = radius / Mathf.Sin(fovRad * 0.5f);
        distance *= 1.35f;

        Vector3 direction = new Vector3(-1f, -0.4f, -1f).normalized;
        Vector3 cameraPosition = center - direction * distance;

        previewUtility.camera.transform.position = cameraPosition;
        previewUtility.camera.transform.rotation = Quaternion.LookRotation(center - cameraPosition, Vector3.up);
        previewUtility.camera.nearClipPlane = 0.01f;
        previewUtility.camera.farClipPlane = Mathf.Max(100f, distance * 8f);
    }

    private static Texture2D TextureToReadableTexture(Texture source, int width, int height)
    {
        RenderTexture tempRt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D readable = null;

        try
        {
            Graphics.Blit(source, tempRt);
            RenderTexture.active = tempRt;

            readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            readable.Apply(false, false);
            return readable;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tempRt);
        }
    }
}
