using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;

namespace TextureMerge
{
    internal class UpdateCheck
    {
        public static async void CheckForUpdateAsync(bool forced = false)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            string version = "v" + fvi.FileVersion;
            string content;
            using (WebClient client = new WebClient())
            {
                try
                {
                    client.Headers.Add(HttpRequestHeader.UserAgent, "TextureMerge_webclient");
                    content = await client.DownloadStringTaskAsync(new Uri("https://api.github.com/repos/Fidifis/TextureMerge/releases/latest"));
                }
                catch (WebException ex)
                {
                    // Probbably no internet connection - dont show error message if not forcced
                    if (forced)
                    {
                        MessageDialog.Show("Error when trying to check latest version. Check your internet connection." +
                            Environment.NewLine + ex.Message,
                            "Error", MessageDialog.Type.Error);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    MessageDialog.Show("Error when trying to check latest version" + Environment.NewLine + ex.Message,
                        "Error", MessageDialog.Type.Error);
                    return;
                }
            }

            string latestVersion = GetValue(content, "tag_name");
            var latestVersionNums = GetVersionNums(latestVersion);
            var currentVersionNums = GetVersionNums(version);
            if (CompareVersionNums(latestVersionNums, currentVersionNums) > 0 && (forced || Config.Current.SkipVersion != latestVersion))
            {
                var updateDialog = new UpdateAvailable(latestVersion);
                updateDialog.ShowDialog();
                if (updateDialog.DialogResult == true)
                {
                    try
                    {
                        Process.Start("https://github.com/Fidifis/TextureMerge/releases/latest");
                    }
                    catch (Exception ex)
                    {
                        MessageDialog.Show("Failed to open web browser." + Environment.NewLine + ex.Message,
                            "Error", MessageDialog.Type.Error);
                    }
                }
                else if (updateDialog.Skip)
                {
                    Config.Current.SkipVersion = latestVersion;
                }
            }
            else if (forced)
            {
                MessageDialog.Show("No updates");
            }
        }

        private static uint[] GetVersionNums(string verstion)
        {
            var nums = new List<uint>();

            string r = @"[0-9]+";
            Regex reg = new Regex(r, RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
            MatchCollection mc = reg.Matches(verstion);
            foreach (Match m in mc)
            {
               uint n = uint.Parse(m.Groups[0].Value);
                nums.Add(n);
            }

            return nums.ToArray();
        }

        private static int CompareVersionNums(uint[] version1, uint[] version2)
        {
            if (version1.Length == 0 && version2.Length == 0)
                return 0;
            else if (version1.Length == 0)
                return -1;
            else if (version2.Length == 0)
                return 1;

            int i = 0;
            for (; i < version1.Length && i < version2.Length; i++)
            {
                if (version1[i] > version2[i])
                    return 1;
                else if (version1[i] < version2[i])
                    return -1;
            }

            if (i < version1.Length)
                return 1;
            else if (i < version2.Length)
                return -1;

            return 0;
        }

        private static string GetValue(string content, string key)
        {
            int start, end;
            start = content.IndexOf(key, 0);
            end = content.IndexOf(",", start);
            return content.Substring(start, end - start).Split(':')[1].Trim().Replace("\"", "");
        }
    }
}
