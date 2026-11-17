// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    [Serializable]
    public class JSpellConfig
    {
        public bool forceUpperCase = false;
        public bool ignoreIrregularCaps = false;
        public bool ignoreFirstCaps = false;
        public bool ignoreUpper = false;
        public bool ignoreDouble = true;
        public bool ignoreWordsWithNumbers = false;
    }

    [Serializable]
    public class JSpellRequest
    {
        public string language;
        public string fieldvalues;
        public JSpellConfig config = new JSpellConfig();
    }

    [Serializable]
    public class JSpellError
    {
        public string word;
        public int position;
        public string[] suggestions;
    }

    [Serializable]
    public class JSpellElement
    {
        public int id;
        public JSpellError[] errors;
    }

    [Serializable]
    public class JSpellResponse
    {
        public int spellingErrorCount;
        public JSpellElement[] elements;
    }

    [Serializable]
    internal class JSpellMessage
    {
        public string message;
    }

    /// <summary>
    /// Sends text to the JSpell Checker API on RapidAPI. Requests don't block the editor.
    /// The API key is never logged.
    /// </summary>
    public static class JSpellClient
    {

        public const string Host = "jspell-checker.p.rapidapi.com";
        public const string Url = "https://" + Host + "/check";
        public const string ApiPageUrl = "https://rapidapi.com/page-scholar-inc-page-scholar-inc-default/api/jspell-checker";

        /// <summary>The API's limit on characters per request.</summary>
        public const int MaxRequestCharacters = 100000;

        private const int TimeoutSeconds = 60;

        public static readonly string[] LanguageCodes = { "enUS", "enGB", "enCA", "enAU", "enUSMedical", "frFR", "esES", "enesUS", "deDE", "itIT" };

        public static readonly string[] LanguageNames =
        {
            "English (US)", "English (UK)", "English (Canada)", "English (Australia)", "English (US, Medical)",
            "French", "Spanish", "English + Spanish", "German", "Italian"
        };

        public static string GetLanguageName(string code)
        {
            var index = Array.IndexOf(LanguageCodes, code);
            return (index >= 0) ? LanguageNames[index] : code;
        }

        /// <summary>
        /// Maps a Dialogue System language code, such as the "fr" in a "Menu Text fr" field, to
        /// a JSpell language code. Returns false if JSpell doesn't support the language.
        /// </summary>
        public static bool TryMapLanguageCode(string code, out string jspellCode)
        {
            jspellCode = null;
            if (string.IsNullOrEmpty(code)) return false;
            if (Array.IndexOf(LanguageCodes, code) >= 0)
            {
                jspellCode = code;
                return true;
            }
            var normalized = code.Trim().Replace('_', '-').ToLowerInvariant();
            switch (normalized)
            {
                case "en":
                case "en-us":
                    jspellCode = "enUS";
                    break;
                case "en-gb":
                case "en-uk":
                    jspellCode = "enGB";
                    break;
                case "en-ca":
                    jspellCode = "enCA";
                    break;
                case "en-au":
                    jspellCode = "enAU";
                    break;
                default:
                    var dash = normalized.IndexOf('-');
                    var language = (dash >= 0) ? normalized.Substring(0, dash) : normalized;
                    switch (language)
                    {
                        case "fr": jspellCode = "frFR"; break;
                        case "es": jspellCode = "esES"; break;
                        case "de": jspellCode = "deDE"; break;
                        case "it": jspellCode = "itIT"; break;
                    }
                    break;
            }
            return jspellCode != null;
        }

        /// <summary>A request in progress, which the caller can abort.</summary>
        public class Call
        {
            internal UnityWebRequest webRequest;
            internal bool isAborted;
            internal bool isDone;

            /// <summary>The HTTP status code, once the request is done.</summary>
            public long responseCode { get; internal set; }

            /// <summary>True if the request was refused because of the rate limit or monthly quota.</summary>
            public bool isTooManyRequests { get { return responseCode == 429; } }

            /// <summary>Aborts the request. Its done callback isn't called.</summary>
            public void Abort()
            {
                if (isDone || isAborted) return;
                isAborted = true;
                if (webRequest != null) webRequest.Abort();
            }
        }

        /// <summary>
        /// Sends a request. Calls done with the response, or with null and an error message.
        /// </summary>
        public static Call Send(string apiKey, JSpellRequest request, Action<JSpellResponse, string> done)
        {
            var webRequest = new UnityWebRequest(Url, UnityWebRequest.kHttpVerbPOST);
            webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)));
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.timeout = TimeoutSeconds;
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("X-RapidAPI-Key", apiKey);
            webRequest.SetRequestHeader("X-RapidAPI-Host", Host);
            var call = new Call() { webRequest = webRequest };
            var operation = webRequest.SendWebRequest();
            operation.completed += _ =>
            {
                call.isDone = true;
                JSpellResponse response = null;
                string error = null;
                try
                {
                    if (call.isAborted) return;
                    call.responseCode = webRequest.responseCode;
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        response = ParseResponse(webRequest.downloadHandler.text, out error);
                    }
                    else
                    {
                        error = GetErrorMessage(webRequest);
                    }
                }
                finally
                {
                    call.webRequest = null;
                    webRequest.Dispose();
                }
                done?.Invoke(response, error);
            };
            return call;
        }

        public static JSpellResponse ParseResponse(string json, out string error)
        {
            error = null;
            try
            {
                var response = JsonUtility.FromJson<JSpellResponse>(json);
                if (response == null) error = "JSpell returned an empty response.";
                return response;
            }
            catch (ArgumentException)
            {
                error = "JSpell returned a response that couldn't be read.";
                return null;
            }
        }

        private static string GetErrorMessage(UnityWebRequest webRequest)
        {
            var serverMessage = GetServerMessage(webRequest);
            switch (webRequest.responseCode)
            {
                case 401:
                case 403:
                    return "JSpell refused the API key. Check the key in Settings and that it's subscribed to the JSpell Checker API on RapidAPI." +
                        (string.IsNullOrEmpty(serverMessage) ? string.Empty : " (" + serverMessage + ")");
                case 429:
                    return "The JSpell request limit was reached (rate limit or monthly quota). Try again later, or upgrade the RapidAPI plan." +
                        (string.IsNullOrEmpty(serverMessage) ? string.Empty : " (" + serverMessage + ")");
                case 413:
                    return "The JSpell request was too large.";
            }
            if (webRequest.result == UnityWebRequest.Result.ConnectionError)
            {
                return "Couldn't connect to JSpell: " + webRequest.error;
            }
            return string.Format("JSpell request failed ({0}): {1}", webRequest.responseCode,
                string.IsNullOrEmpty(serverMessage) ? webRequest.error : serverMessage);
        }

        private static string GetServerMessage(UnityWebRequest webRequest)
        {
            var text = (webRequest.downloadHandler != null) ? webRequest.downloadHandler.text : null;
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                var message = JsonUtility.FromJson<JSpellMessage>(text);
                if (message != null && !string.IsNullOrEmpty(message.message)) return message.message;
            }
            catch (ArgumentException)
            {
                // Not JSON.
            }
            return (text.Length > 200) ? text.Substring(0, 200) : text;
        }

    }

}

#endif
