using System.Text;
using System.Text.RegularExpressions;

namespace SignalBackupBeautifier;

/// <summary>
/// This class is used to beautify Signal backups.
/// It creates two HTML files for each month (one Chat, one Media) and an index file.
///
/// For the one special person, for which the entire universe conspired so that we found each other.
/// </summary>
internal class BackupBeautifier {

    public enum DirectionType {

        IN,
        OUT

    }

    private const char   VERSION_LINE_START       = '|';
    private const string QUOTE_LINE_START         = ">";
    private const string MONTH_PLACEHOLDER        = "%%%";
    private const string DAY_HEADLINE_PLACEHOLDER = "§§§";

    private const string HTML_END_TEMPLATE = @"<a class=""idxLink"" href=""index.htm"">Index</a>
        </main></div><br><br>
        <br><br></body></html>";

    private static readonly string HTML_TEMPLATE = $@"<!doctype html>
<html lang=""de"">
<head>
  <meta charset=""utf-8"">
  <title>Signal Backup {MONTH_PLACEHOLDER}</title>
  <meta name=""viewport"" content=""width=device-width"">
  <link rel=""stylesheet"" href=""style.css"" type=""text/css"">
</head>
<body>
<div id=""chat"">
  
    <div class=""msger-header-title"">
      <i class=""fas fa-comment-alt""></i><a href=""index.htm""> Signal Backup </a>
    </div>
    <div class=""msger-header-options"">
      <span><i class=""fas fa-cog""></i></span>
    </div>
    <main class=""msger-chat"">

  ";

    private static bool            removeNums;
    private static string?         attFolder;
    private static string?         outPath;
    private static string?         fullAttPath;
    private static List<FileInfo>? allAttachments;

    public string? ReactionHtml {
        get {
            if ( Reaction != null ) return Reaction.Remove(3) + "<small>" + Reaction.Substring(3) + "</small>";
            return null;
        }
    }

    #region Fields

    public string?                    From;
    public DirectionType              Type;
    public DateTime                   Sent;
    public DateTime?                  EditedTime;
    public DateTime?                  Received;
    public string?                    Message;
    public string?                    Edited;
    public int?                       EditCnt;
    public string?                    Reaction;
    public Dictionary<string, string> Attachments = new();

    public BackupBeautifier? Quote;

    #endregion

    public static void Convert(string file, bool removeNumbers = true, bool minify = true) {
        removeNums = removeNumbers;
        attFolder  = Path.GetFileNameWithoutExtension(file);

        outPath = Path.GetDirectoryName(file);
        if ( outPath == null ) throw new Exception("outPath is null");
        fullAttPath = Path.Combine(outPath, attFolder);

        DirectoryInfo di = new(fullAttPath);
        allAttachments = new List<FileInfo>(di.GetFiles());

        string[] lines = File.ReadAllLines(file, Encoding.UTF8);

        List<BackupBeautifier> signals = new();
        BackupBeautifier?      sbb     = null;
        //int                          cnt     = 0;

        bool skipOldVersions = false;

        foreach ( string lx in lines ) {
            if ( sbb == null &&
                 !lx.StartsWith("From: ") ) continue; // Bis zur ersten Nachricht überspringen

            if ( skipOldVersions && lx.StartsWith(VERSION_LINE_START.ToString()) ) continue;

            string lineToUse = lx.TrimStart(VERSION_LINE_START).Trim();

            if ( lineToUse.StartsWith("From: ") ) {
                if ( sbb != null ) signals.Add(sbb);
                skipOldVersions = false;

                sbb = new BackupBeautifier();
                sbb.AddLine(lineToUse, out _);
                continue;
            }

            if ( sbb == null ) throw new Exception("das sollte nicht passieren!");

            if ( lineToUse.StartsWith(QUOTE_LINE_START) ) { // Quote Mode
                if ( sbb.Quote == null ) sbb.Quote = new BackupBeautifier();
                sbb.Quote.AddLine(lineToUse.Substring(1).Trim(), out _);
                continue;
            }

            sbb.AddLine(lineToUse, out bool skip);
            if ( skip ) skipOldVersions = true;

            //#warning remove!!!
            //if ( cnt++ > 1400 ) break;
        }

        FixQuotes(signals);

        // Datei pro Monat und ein Index

        Dictionary<string, string> htmlFiles       = new();
        string                     indexDoc        = HTML_TEMPLATE.Replace(MONTH_PLACEHOLDER, "Übersicht") + "<div class=\"idx\"><ul>";
        string                     currMonth       = "";
        string                     currHtml        = HTML_TEMPLATE;
        string                     aktMonth        = "";
        string                     currHtmlGallery = HTML_TEMPLATE + "<div class=\"att-gal\">";
        int                        lastDay         = 0;
        int                        nextDay         = 0;
        string?                    lastDayString   = null;
        int                        theDayBefore    = 0;

        foreach ( BackupBeautifier toHtml in signals ) {
            if ( String.IsNullOrEmpty(currMonth) ) currMonth = "" + toHtml.Sent.Year + "-" + toHtml.Sent.Month;
            aktMonth = "" + toHtml.Sent.Year + "-" + toHtml.Sent.Month;

            if ( currMonth != aktMonth ) {
                SetDayHeadline(lastDayString, nextDay, lastDay, theDayBefore, ref currHtml, true);
                ProcessMonth(currMonth, htmlFiles, ref currHtml, ref currHtmlGallery, ref indexDoc);
                currMonth     = aktMonth;
                lastDay       = 0;
                lastDayString = null;
                theDayBefore  = 0;
            }

            string tmpDay = GetHeadlineDayString(toHtml.Sent);
            nextDay = toHtml.Sent.Day;
            if ( lastDayString != tmpDay ) SetDayHeadline(lastDayString, nextDay, lastDay, theDayBefore, ref currHtml, false);

            currHtml += toHtml.GetHtml();

            if ( toHtml.Attachments.Count > 0 ) currHtmlGallery += toHtml.GetAttachmentsHtml();

            if ( lastDay != toHtml.Sent.Day ) {
                theDayBefore  = lastDay;
                lastDay       = toHtml.Sent.Day;
                lastDayString = GetHeadlineDayString(toHtml.Sent);
            }
        }

        SetDayHeadline(lastDayString, nextDay, lastDay, theDayBefore, ref currHtml, true);
        ProcessMonth(aktMonth, htmlFiles, ref currHtml, ref currHtmlGallery, ref indexDoc);

        indexDoc += "</ul></div></main></div></body></html>";

        htmlFiles.Add("index.htm", indexDoc);

        foreach ( string fName in htmlFiles.Keys ) {
            string toWrite = htmlFiles[fName];

            // Entfernt Leerzeichen, Tabs und Zeilenumbrüche zwischen HTML-Tags
            if ( minify ) toWrite = Regex.Replace(toWrite, @">\s+<", "><");

            File.WriteAllText(Path.Combine(outPath, fName), toWrite, Encoding.UTF8);
        }
    }

    private static string GetHeadlineDayString(DateTime dt) {
        return dt.ToLongDateString();
    }

    private static void SetDayHeadline(string? lastDayString, int nextDay, int lastDay, int theDayBefore, ref string currHtml, bool isLast) {
        if ( String.IsNullOrEmpty(lastDayString) ) {
            currHtml += DAY_HEADLINE_PLACEHOLDER;
            return;
        }

        string hdLn = $"<span class=\"day-lnk\" id=\"d_{lastDay}\">";

        if ( theDayBefore > 0 ) {
            string linkToPrev = "#d_" + theDayBefore;
            hdLn += $"<a href=\"{linkToPrev}\">&uArr;</a> ";
        }
        else hdLn += "<a> </a> ";

        hdLn += lastDayString;

        if ( !isLast ) {
            string linkToNext = "#d_" + nextDay;
            hdLn += $" <a href=\"{linkToNext}\">&dArr;</a>";
        }
        else hdLn += "<a> </a> ";

        hdLn += "</span>";

        currHtml = currHtml.Replace(DAY_HEADLINE_PLACEHOLDER, hdLn);
        if ( !isLast ) currHtml += DAY_HEADLINE_PLACEHOLDER;
    }

    private static void FixQuotes(List<BackupBeautifier> signals) {
        // Anhänge bei Quotes stimmen fast nie, weil die keine Dateigröße haben,
        // also suchen wir das original (anhand der Sendezeit) und holen uns da das richtige Attachment
        foreach ( BackupBeautifier s in signals ) {
            if ( s.Quote == null ) continue;

            BackupBeautifier? orig   = signals.FirstOrDefault(o => o.Sent == s.Quote.Sent);
            if ( orig == null ) orig = signals.FirstOrDefault(o => o.EditedTime == s.Quote.Sent);
            if ( orig != null ) s.Quote = orig;
            else Log.Info($"Quote not found for sent: {s.Quote.Sent}, probably no real quote");
        }
    }

    private static void ProcessMonth(string currMonth, Dictionary<string, string> htmlFiles, ref string currHtml, ref string currHtmlGallery, ref string indexDoc) {
        Log.Info("Processed: " + currMonth);

        currHtml        = currHtml.Replace(MONTH_PLACEHOLDER, currMonth);
        currHtmlGallery = currHtmlGallery.Replace(MONTH_PLACEHOLDER, "Medien: " + currMonth);

        currHtml        += HTML_END_TEMPLATE;
        currHtmlGallery += "</div>";
        currHtmlGallery += HTML_END_TEMPLATE;

        string filename    = currMonth + ".htm";
        string filenameGal = currMonth + "_media.htm";

        htmlFiles.Add(filename, currHtml);
        htmlFiles.Add(filenameGal, currHtmlGallery);
        indexDoc += $"<li><a href=\"{filename}\">{currMonth}</a> &nbsp; - &nbsp; " +
                    $"<a href=\"{filenameGal}\">&#10064; Media</a>"                +
                    "</li>";

        currHtml        = HTML_TEMPLATE;
        currHtmlGallery = HTML_TEMPLATE + "<div class=\"att-gal\">";
    }

    private string GetHtml() {
        string msgHtml = "";

        // DirectionType → Links rechts, wer steht wo?

        msgHtml += $@"<div class=""msg {( Type == DirectionType.OUT ? "left" : "right" )}-msg"">
    <div class=""msg-bubble"">";

        if ( Quote != null ) {
            msgHtml += "<div class=\"msg-quote\">";
            msgHtml += Quote.GetHtml();
            msgHtml += "</div>";
        }

        msgHtml += $@"<div class=""msg-info"">
          <div class=""msg-info-name"">{From}{( String.IsNullOrEmpty(Edited) ? "" : " <i>[" + Edited + "]</i>" )}</div>
          <div class=""msg-info-time"">{Sent.ToShortDateString() + " - " + Sent.ToShortTimeString()}</div>
        </div>

        <div class=""msg-text"">{Message}";

        if ( String.IsNullOrWhiteSpace(Message) &&
             Attachments.Count == 0 ) msgHtml += "<img class=\"msg-sticker\" src=\"Sticker.png\">";

        msgHtml += "</div>";

        if ( !String.IsNullOrEmpty(Reaction) ) msgHtml += "<div class=\"msg-reaction\">"    + ReactionHtml         + "</div>";
        if ( Attachments.Count > 0 ) msgHtml           += "<div class=\"msg-attachments\">" + GetAttachmentsHtml() + "</div>";

        msgHtml += @"</div>
                  </div>
";
        return msgHtml;
    }

    private string GetAttachmentsHtml() {
        string att = "";
        foreach ( string name in Attachments.Keys ) {
            string type                       = Attachments[name];
            string attUrl                     = attFolder + "/" + name;
            if ( type.Contains("image") ) att += "<a href=\""                      + attUrl + "\"><img src=\"" + attUrl + "\"></a>";
            if ( type.Contains("video") ) att += "<video controls> <source src=\"" + attUrl + "\" type=\"video/mp4\"></video>";
            if ( type.Contains("audio") ) att += "<audio controls> <source src=\"" + attUrl + "\" type=\"audio/mp3\"></audio>";
        }

        return att;
    }

    private void AddLine(string lineToUse, out bool skipVersions) {
        skipVersions = false;

        if ( lineToUse.StartsWith("From: ") ) {
            From = lineToUse.Substring(6);
            if ( removeNums && From.Contains("(") ) From = From.Remove(From.IndexOfO("("));
            return;
        }

        if ( lineToUse.StartsWith("Type: ") ) {
            Type = lineToUse.Substring(6) == "outgoing" ? DirectionType.OUT : DirectionType.IN;
            return;
        }

        if ( lineToUse.StartsWith("Sent: ") ) {
            if ( !EditCnt.HasValue ||
                 EditCnt == 0 ) Sent = DateTime.Parse(lineToUse.Substring(6));
            else EditedTime          = DateTime.Parse(lineToUse.Substring(6));
            return;
        }

        if ( lineToUse.StartsWith("Received: ") ) {
            Received = DateTime.Parse(lineToUse.Substring(10));
            return;
        }

        if ( lineToUse.StartsWith("Reaction: ") ) {
            Reaction = lineToUse.Substring(10);
            return;
        }

        if ( lineToUse.StartsWith("Edited: ") ) {
            Edited  = lineToUse.Substring(8).Trim();
            EditCnt = Int32.Parse(Edited.Split(' ')[0]);
            return;
        }

        if ( lineToUse.StartsWith("Attachment: ") ) {
            if ( allAttachments == null ) throw new Exception("allAttachments is null");

            string a    = lineToUse.Substring(12).Trim();
            string name = a.Remove(a.IndexOfO("(")).Trim();
            string type = a.Substring(a.IndexOfO("(")).Trim();

            // Attachment: no filename (image/jpeg, 262101 bytes)
            // jetzt immer per Größe, wir haben haufenweise gleich benannte Bilder die dann durchnummeriert sind :/
            if ( a.IndexOfO(",", a.IndexOfO("(")) > 0 ) {
                Log.Debug("Attachment, trying by size...");

                string size = a.Substring(a.IndexOfO(",", a.IndexOfO("(")) + 1);
                size = size.Remove(size.IndexOfO(" bytes")).Trim();
                int       targetSize = Int32.Parse(size);
                FileInfo? foundFile  = allAttachments.FirstOrDefault(f => f.Length == targetSize);
                if ( foundFile != null ) {
                    name = foundFile.Name;
                    Log.Debug($"Attachment 'no filename', found by size: {foundFile.Name}; {foundFile.Length}");
                }
            }

            string? nameToUse = name;
            int     cnt       = 2;
            while ( Attachments.ContainsKey(nameToUse) ) {
                Log.Debug($"Attachment name ({nameToUse}) in use, trying to enumerate...");

                string fn  = Path.GetFileNameWithoutExtension(name);
                string ext = Path.GetExtension(name);
                nameToUse = fn + "-" + cnt++ + ext;

                FileInfo? foundFile = allAttachments.FirstOrDefault(f => f.Name == nameToUse);
                if ( foundFile == null ) {
                    nameToUse = null;
                    break;
                }
            }

            if ( nameToUse != null ) {
                Log.Debug($"Attachment name found, adding: {nameToUse}");

                Attachments.Add(nameToUse, type);
            }

            return;
        }

        if ( lineToUse.StartsWith("Version: ") ) {
            if ( !lineToUse.StartsWith("Version: " + EditCnt) ) skipVersions = true;

            return;
        }

        Message += lineToUse;
        if ( !String.IsNullOrWhiteSpace(lineToUse) ) Message += Environment.NewLine;
    }

}

internal class Log {

    public static void Debug(string msg) {
        Console.WriteLine(msg);
    }

    public static void Info(string msg) {
        Console.WriteLine(msg);
    }

}

public static class StringExtensions {

    public static int IndexOfO(this string source, string value) {
        return source.IndexOf(value, StringComparison.Ordinal);
    }

    public static int IndexOfO(this string source, string value, int startIdx) {
        return source.IndexOf(value, startIdx, StringComparison.Ordinal);
    }

}
