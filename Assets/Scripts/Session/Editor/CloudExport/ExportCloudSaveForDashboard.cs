#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Unity.Services.Core.Editor;

// Pulls Player Cloud Save for the Quest anonymous ID and writes it for the therapist website.
public static class ExportCloudSaveForDashboard
{
    const string ProjectId = "35a0f424-a2ce-45a4-b2f3-5a9e3b9d734b";
    const string EnvironmentId = "b2efe99c-adb3-43d6-ac05-fe9ad3802e83";
    const string PlayerId = "7EOyuU9AXzVZDR3vtQcDANgahjFD";

    [MenuItem("XRHands/Export Cloud Save to Therapist Dashboard")]
    static void Export()
    {
        ExportAsync();
    }

    static async void ExportAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(CloudProjectSettings.accessToken))
            {
                EditorUtility.DisplayDialog(
                    "XRHands",
                    "Sign in to Unity in the Editor first (account icon, top-right), using the same account that can see Cloud Save in the Unity Dashboard.",
                    "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("XRHands", "Reading Cloud Save…", 0.2f);
            var tokens = new AccessTokens();
            string gateway = await tokens.GetServicesGatewayTokenAsync();
            if (string.IsNullOrEmpty(gateway))
                throw new Exception("Could not get a Unity Services token. Sign in to Unity in the Editor and try again.");

            var rows = new List<string>();
            rows.AddRange(await FetchItemRows(gateway, false));
            try
            {
                rows.AddRange(await FetchItemRows(gateway, true));
            }
            catch
            {
                // Public copies are optional.
            }

            if (rows.Count == 0)
                throw new Exception(
                    "No rws_session_* keys came back for player " + PlayerId +
                    ". Confirm that ID in Unity Dashboard → Cloud Save → Player Data.");

            string destDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../dashboard/public"));
            Directory.CreateDirectory(destDir);
            string dest = Path.Combine(destDir, "rws-cloud-save.json");
            string json = "{\"playerId\":\"" + PlayerId + "\",\"results\":[" + string.Join(",", rows) + "]}";
            File.WriteAllText(dest, json, new UTF8Encoding(false));

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog(
                "XRHands",
                "Exported " + rows.Count + " Cloud Save item(s) to:\n" + dest +
                "\n\nThe public website cannot read this laptop file by itself. On the therapist console click Import and choose rws-cloud-save.json, then sign in with 0000 / 0000.",
                "OK");
            Debug.Log("[XRHands] Wrote " + dest);
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("[XRHands] Cloud Save export failed: " + e.Message);
            EditorUtility.DisplayDialog("XRHands", "Export failed:\n" + e.Message, "OK");
        }
    }

    static async Task<List<string>> FetchItemRows(string token, bool pub)
    {
        string suffix = pub ? "/public/items" : "/items";
        string after = null;
        var rows = new List<string>();
        for (int page = 0; page < 25; page++)
        {
            string url =
                "https://services.api.unity.com/cloud-save/v1/data/projects/" + ProjectId +
                "/environments/" + EnvironmentId + "/players/" + PlayerId + suffix +
                "?prefix=rws_session";
            if (!string.IsNullOrEmpty(after))
                url += "&after=" + UnityWebRequest.EscapeURL(after);

            string body = await GetText(url, token);
            var pageRows = ExtractJsonArrayObjects(body, "results");
            if (pageRows.Count == 0 && page == 0)
            {
                url =
                    "https://services.api.unity.com/cloud-save/v1/data/projects/" + ProjectId +
                    "/environments/" + EnvironmentId + "/players/" + PlayerId + suffix;
                body = await GetText(url, token);
                pageRows = ExtractJsonArrayObjects(body, "results");
                pageRows.RemoveAll(row =>
                    row.IndexOf("\"rws_session", StringComparison.Ordinal) < 0);
            }

            rows.AddRange(pageRows);
            if (pageRows.Count == 0) break;
            after = ExtractJsonStringField(pageRows[pageRows.Count - 1], "key");
            if (string.IsNullOrEmpty(after) || body.IndexOf("\"next\"", StringComparison.Ordinal) < 0)
                break;
        }

        return rows;
    }

    static Task<string> GetText(string url, string token)
    {
        var tcs = new TaskCompletionSource<string>();
        var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", "Bearer " + token);
        req.SetRequestHeader("Accept", "application/json");
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    tcs.TrySetException(new Exception(req.responseCode + " " + req.error + " " + req.downloadHandler.text));
                    return;
                }

                tcs.TrySetResult(req.downloadHandler.text ?? "{}");
            }
            finally
            {
                req.Dispose();
            }
        };
        return tcs.Task;
    }

    static List<string> ExtractJsonArrayObjects(string json, string arrayName)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(json)) return list;
        string needle = "\"" + arrayName + "\"";
        int nameAt = json.IndexOf(needle, StringComparison.Ordinal);
        if (nameAt < 0) return list;
        int bracket = json.IndexOf('[', nameAt);
        if (bracket < 0) return list;

        int depth = 0;
        int start = -1;
        bool inString = false;
        bool escape = false;
        for (int p = bracket; p < json.Length; p++)
        {
            char c = json[p];
            if (inString)
            {
                if (escape) escape = false;
                else if (c == '\\') escape = true;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (p == bracket && c == '[')
            {
                depth = 1;
                continue;
            }

            if (c == '{')
            {
                if (depth == 1) start = p;
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 1 && start >= 0)
                {
                    list.Add(json.Substring(start, p - start + 1));
                    start = -1;
                }
            }
            else if (c == ']' && depth == 1)
            {
                break;
            }
        }

        return list;
    }

    static string ExtractJsonStringField(string jsonObject, string field)
    {
        string needle = "\"" + field + "\"";
        int at = jsonObject.IndexOf(needle, StringComparison.Ordinal);
        if (at < 0) return null;
        int colon = jsonObject.IndexOf(':', at);
        int q1 = jsonObject.IndexOf('"', colon + 1);
        if (q1 < 0) return null;
        int q2 = jsonObject.IndexOf('"', q1 + 1);
        if (q2 < 0) return null;
        return jsonObject.Substring(q1 + 1, q2 - q1 - 1);
    }
}
#endif
