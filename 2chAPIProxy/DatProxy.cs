﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using System.Web;
using Fiddler;
using System.CodeDom.Compiler;
using System.Reflection;
using _2chAPIProxy.HtmlConverter;
using _2chAPIProxy.APIMediator;

namespace _2chAPIProxy
{
    class IPAuthData
    {
        public bool Auth = false;
        public String nonce = "";
    }

    public class DatProxy
    {
        Dictionary<String, String> Cookie = new Dictionary<string, string>();
        Dictionary<String, IPAuthData> AuthIPList = new Dictionary<string, IPAuthData>();
        Regex Check2churi = new Regex(@"^(\w+?)\.((?:2|5)ch\.net|bbspink\.com)$", RegexOptions.Compiled);
        Regex CheckDaturi = new Regex(@"^https?:\/\/(\w+?)\.((?:2|5)ch\.net|bbspink\.com)\/(\w+?)\/dat\/(\d+?)\.dat", RegexOptions.Compiled);
        Regex CheckWriteuri = new Regex(@"^https?:\/\/\w+?(\.(?:2|5)ch.net|\.bbspink.com)(?::\d{2,})?\/test\/(?:sub)?bbs\.cgi", RegexOptions.Compiled);
        Regex CheckKakouri = new Regex(@"(^https?:\/\/rokka\.((?:2|5)ch\.net|bbspink\.com)\/(\w+?)\/(\w+?)\/(\d+?)\/.+|http:\/\/\w+?.(2|5)ch\.net\/test\/offlaw2\.so.+)", RegexOptions.Compiled);
        Regex CheckKakouri2 = new Regex(@"^https?:\/\/((?:\w+?)\.(?:(?:2|5)ch\.net|bbspink\.com))\/(\w+?)\/kako\/(?:\d{4}\/\d{5}|\d{3})\/(\d+?)\.dat", RegexOptions.Compiled);
        Regex CheckOldBe = new Regex(@"^https?://(?:be.(?:2|5)ch.net/(?:test/)?(login|index).php)", RegexOptions.Compiled);
        Regex CheckShitaraba = new Regex(@"^http://jbbs.(shitaraba.net|livedoor.jp)", RegexOptions.Compiled);
        //Regex CheckShitarabaPost = new Regex(@"^https?://jbbs.(shitaraba.net|livedoor.jp)(/bbs/(rawmode|read).cgi/\w+/\d+/\d+|.+?/write.cgi.*?)", RegexOptions.Compiled);
        Regex CheckShitarabaPost = new Regex(@"^https?://jbbs.(shitaraba.net|livedoor.jp)/.+?/write.cgi", RegexOptions.Compiled);
        //Regex CheckItauri = new Regex(@"^https?:\/\/(\w+?)\.(2ch\.net|bbspink\.com)\/(\w+?)/?$", RegexOptions.Compiled);
        Regex CheckItauri = new Regex(@"^https?://\w+?\.((?:2|5)ch\.net|bbspink\.com)/\w+(/?$|/subject.txt$)", RegexOptions.Compiled);
        Regex BBSMenuReplace = new Regex(@"^https?://menu\.(?:2|5)ch\.net/bbsmenu.html", RegexOptions.Compiled);
        volatile bool SIDNowUpdate = false;


        public IAPIMediator APIMediator { get; set; }

        public IHtmlConverter HtmlConverter { get; set; }

        public Dictionary<string, string> WriteHeader { get; set; }
        //public HTMLtoDat htmlconverter { get; set; }
        public String Proxy { get; set; }
        public String WriteUA { get; set; }
        //public String NormalUA { get; set; }
        public String user { get; set; }
        public String pw { get; set; }
        public bool GetHTML { get; set; }
        public bool AllowWANAccese { get; set; }
        public bool CangeUARetry { get; set; }
        public bool OfflawRokkaPerm { get; set; }
        public bool gZipRes { get; set; }
        public bool ChunkRes { get; set; }
        public bool SocksPoxy { get; set; }
        public bool OnlyORPerm { get; set; }
        public bool CRReplace { get; set; }
        public bool KakolinkPerm { get; set; }
        public bool AllUAReplace { get; set; }
        public bool BeLogin { get; set; }

        public DatProxy(String Akey, String Hkey, String ua1, String sidUA, String ua2, String RID, String RPW, String ProxyAddrese)
        {
            //System.Threading.Timer GetSid = null;
            //GetSid = new System.Threading.Timer((e) =>
            //{
            //    using (GetSid)
            //    {
            //        APIMediator.RouninID = RID;
            //        APIMediator.RouninPW = RPW;
            //        APIMediator.Init(Akey, Hkey, ua1, sidUA, ua2, ProxyAddrese);
            //        //APIMediator = new APIAccess(Akey, Hkey, ua1, sidUA, ua2, RID, RPW, ProxyAddrese);
            //    }
            //}, null, 0, System.Threading.Timeout.Infinite);
            //Fiddler設定変更
            Fiddler.CONFIG.bReuseClientSockets = true;
            Fiddler.CONFIG.bReuseServerSockets = true;
            //正規表現キャッシュサイズ
            Regex.CacheSize = 75;
            GetHTML = true;
            AllowWANAccese = false;

            FiddlerApplication.BeforeRequest += (oSession) =>
            {
                try
                {
                    if (Proxy != "") oSession["X-OverrideGateway"] = (SocksPoxy) ? ("socks=" + Proxy) : (Proxy);
                    if (AllowWANAccese && !oSession.clientIP.Contains("127.0.0.1"))
                    {
                        //WANアクセス有効時の認証と識別
                        if (!WANAcceseAuth(ref oSession)) return;
                    }
                    if (Check2churi.IsMatch(oSession.hostname))
                    {
                        //元のURLが2chか5chか
                        bool is2ch = oSession.fullUrl.Contains(".2ch.net/");
                        //2ch→5ch置換
                        oSession.fullUrl = oSession.fullUrl.Replace(".2ch.net/", ".5ch.net/");
                        if (oSession.oRequest.headers.Exists("Referer"))
                        {
                            oSession.oRequest.headers["Referer"] = oSession.oRequest.headers["Referer"].Replace(".2ch.net/", ".5ch.net/");
                        }
                        if (CheckDaturi.IsMatch(oSession.fullUrl))
                        {
                            //dat読みをAPIへ
                            oSession.utilCreateResponseAndBypassServer();
                            GetDat(ref oSession, is2ch);
                            return;
                        }
                        else if (GetHTML && (CheckKakouri.IsMatch(oSession.fullUrl) || CheckKakouri2.IsMatch(oSession.fullUrl)))
                        {
                            //offlaw,rokka,kakoリンクのHTML変換応答
                            if (OtherLinkHTMLTrance(ref oSession)) return;
                        }
                        else if (CheckWriteuri.IsMatch(oSession.fullUrl))
                        {
                            if(ViewModel.Setting.PostNoReplace == true)
                            {
                                System.Diagnostics.Debug.WriteLine("書き込み関与を最小限にして書き込み");
                                if (oSession.oRequest.headers["User-Agent"].Contains("gikoNavi") == true)
                                {
                                    oSession.utilReplaceInRequest("\r\n", "");
                                }
                                if (ViewModel.Setting.UseTLSWrite == true) oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
                                if (string.IsNullOrEmpty(ViewModel.Setting.UserAgent3) == false) oSession.oRequest.headers["User-Agent"] = ViewModel.Setting.UserAgent3;
                                oSession.Ignore();
                            }
                            else
                            {
                                //書き込みをバイパスする
                                oSession.utilCreateResponseAndBypassServer();
                                ResPost(oSession, is2ch);
                            }
                            return;
                        }
                        else if (CheckOldBe.IsMatch(oSession.fullUrl))
                        {
                            //Be2.1ログイン処理代行
                            Be21Login(oSession, is2ch);
                            return;
                        }
                        else if (CheckItauri.IsMatch(oSession.fullUrl))
                        {
                            //移転時のhttpsリンクを書き換える
                            Replacehttps(ref oSession, is2ch);
                            return;
                        }
                        else if (BBSMenuReplace.IsMatch(oSession.fullUrl))
                        {
                            //板一覧のリンク前後についているダブルクォート削除
                            //BBSMenuの置き換え
                            BBSMenuURLReplace(ref oSession, is2ch);
                            return;
                        }
                        else if (oSession.fullUrl.Contains("://dig."))
                        {
                            //スレタイ検索(dig.2ch.net)
                            oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
                            if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink)
                            {
                                oSession.bBufferResponse = true;
                                SessionStateHandler BRHandler = null;
                                BRHandler = (ooSession) =>
                                {
                                    FiddlerApplication.BeforeResponse -= BRHandler;
                                    //ooSession.ResponseBody = Encoding.UTF8.GetBytes(HTMLtoDat.ResContentReplace(ooSession.GetResponseBodyAsString()));
                                    ooSession.ResponseBody = Encoding.UTF8.GetBytes(HtmlConverter.ResContentReplace(ooSession.GetResponseBodyAsString()));
                                };
                                FiddlerApplication.BeforeResponse += BRHandler;
                                return;
                            }
                            oSession.Ignore();
                            return;
                        }
                        //APIや書き込み等以外のアクセス時の処理
                        if (ViewModel.Setting.UseTLSWrite) oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
                        if (AllUAReplace) oSession.oRequest.headers["User-Agent"] = WriteUA;
                    }
                    else if (CheckShitaraba.IsMatch(oSession.fullUrl))
                    {
                        if (ViewModel.Setting.UseTLSWrite)
                        {
                            //したらばTLS接続
                            oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
                            oSession["x-OverrideSslProtocols"] = " tls1.0;tls1.1;tls1.2";
                            System.Diagnostics.Debug.WriteLine("HTTPS化：" + oSession.fullUrl);
                        }
                        
                        if (CheckShitarabaPost.IsMatch(oSession.fullUrl))
                        {
                            //したらば書き込みと結果置換
                            ShitarabaPost(oSession);
                            System.Diagnostics.Debug.WriteLine("書き込み置換：" + oSession.fullUrl);
                            return;
                        }

                        oSession.Ignore();
                        return;
                    }

                    //不要ヘッダの削除
                    oSession.oRequest.headers.Remove("Pragma");
                    if (oSession.oRequest.headers.Exists("Proxy-Connection"))
                    {
                        oSession.oRequest.headers["Connection"] = oSession.oRequest.headers["Proxy-Connection"];
                        oSession.oRequest.headers.Remove("Proxy-Connection");
                    }
                    oSession.Ignore();
                }
                catch(Exception err)
                {
                    ViewModel.OnModelNotice($"プロクシ処理中にエラーが発生しました。URL:{oSession.fullUrl}\n{err.ToString()}");
                }
            };
        }

        private void BBSMenuURLReplace(ref Session oSession, bool is2ch)
        {
            if (ViewModel.Setting.UseTLSWrite) oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
            oSession.bBufferResponse = true;
            SessionStateHandler MRHandler = null;
            MRHandler = (ooSession) =>
            {
                FiddlerApplication.BeforeResponse -= MRHandler;
                var html = ooSession.GetResponseBodyAsString();
                var ItaMatches = Regex.Matches(html, $@"<(?:A HREF|a href)={'"'}(?:https?:)?(//\w+?\.(?:2ch\.net|5ch\.net|bbspink\.com)/\w+/?){'"'}>(.+)</(?:A|a)>");
                foreach (Match ita in ItaMatches)
                {
                    String replace = $"<A HREF=http:{ita.Groups[1].Value}>{ita.Groups[2].Value}</A>";
                    html.Replace(ita.Value, replace);
                }
                html = Regex.Replace(html, $@"<(?:A HREF|a href)={'"'}(?:https?:)?(//.+?){'"'}>(.+?)</(?:A|a)>", "<A HREF=http:$1>$2</A>");
                if (is2ch) html = html.Replace(".5ch.net/", ".2ch.net/");
                 ooSession.ResponseBody = Encoding.GetEncoding("shift_jis").GetBytes(html);
            };
            FiddlerApplication.BeforeResponse += MRHandler;
            if (AllUAReplace) oSession.oRequest.headers["User-Agent"] = WriteUA;
            return;
        }

        private void Replacehttps(ref Session oSession, bool is2ch)
        {
            if (ViewModel.Setting.UseTLSWrite) oSession.fullUrl = oSession.fullUrl.Replace("http://", "https://");
            oSession.bBufferResponse = true;
            SessionStateHandler BRHandler = null;
            BRHandler = (ooSession) =>
            {
                FiddlerApplication.BeforeResponse -= BRHandler;
                String itenuri = ooSession.GetResponseBodyAsString();
                if (ooSession.responseCode == 301)
                {
                    ooSession.oResponse.headers.SetStatus(302, "302 Found");
                    if(ooSession.fullUrl.Contains("subject.txt")) ooSession.oResponse.headers["Location"] = "http://www2.2ch.net/live.html";
                }
                if (itenuri.Contains("Change your bookmark") || itenuri.Contains("The document has moved"))
                {
                    itenuri = itenuri.Replace($"{'"'}//", $"{'"'}http://");
                    itenuri = itenuri.Replace("https://", "http://");
                    if (is2ch) itenuri = itenuri.Replace(".5ch.net/", ".2ch.net/");
                    ooSession.ResponseBody = Encoding.GetEncoding("shift_jis").GetBytes(itenuri);
                }
                if (!String.IsNullOrEmpty(ooSession.oResponse.headers["Location"]))
                {
                    string locate = ooSession.oResponse.headers["Location"].Replace("https://", "http://");
                    if (is2ch) locate = locate.Replace(".5ch.net/", ".2ch.net/");
                    ooSession.oResponse.headers["Location"] = locate;
                }
            };
            FiddlerApplication.BeforeResponse += BRHandler;
            if (AllUAReplace) oSession.oRequest.headers["User-Agent"] = WriteUA;
        }

        bool WANAcceseAuth(ref Session oSession)
        {
            try
            {
                //接続してきたアドレスは認証済みかチェック
                //if (!AuthIP.ContainsKey(oSession.clientIP) || !AuthIP[oSession.clientIP])
                if (!AuthIPList.ContainsKey(oSession.clientIP) || !AuthIPList[oSession.clientIP].Auth)
                {
                    //認証されていない時
                    //if (AuthIP.ContainsKey(oSession.clientIP))
                    if (AuthIPList.ContainsKey(oSession.clientIP))
                    {
                        //帰ってきたMD5をチェックする
                        String Res = oSession.oRequest.headers["Authorization"];
                        String /*nonce = Regex.Match(Res, @".*(?<!c)nonce=" + '"' + "(.+?)" + '"').Groups[1].Value,*/
                            uri = Regex.Match(Res, @".*uri=" + '"' + "(.+?)" + '"').Groups[1].Value,
                            nc = Regex.Match(Res, @".*nc=(.+?),").Groups[1].Value,
                            cnonce = Regex.Match(Res, @".*cnonce=" + '"' + "(.+?)" + '"').Groups[1].Value,
                            resp = Regex.Match(Res, @".*response=" + '"' + "(.+?)" + '"').Groups[1].Value;
                        String method = oSession.RequestMethod;
                        String A1 = CMD5(user + ":2chAPIProxy Auth:" + pw);
                        String A2 = CMD5(method + ":" + uri);
                        String hash = CMD5(A1 + ":" + AuthIPList[oSession.clientIP].nonce + ":" + nc + ":" + cnonce + ":" + "auth" + ":" + A2);
                        if (hash == resp)
                        {
                            ViewModel.OnModelNotice(oSession.clientIP + "を認証");
                            AuthIPList[oSession.clientIP].nonce = "";
                            AuthIPList[oSession.clientIP].Auth = true;
                            //AuthIP[oSession.clientIP] = true;
                            if (uri != "/" && uri != "/?") return true;
                            oSession.utilCreateResponseAndBypassServer();
                            oSession.oResponse.headers.HTTPResponseCode = 200;
                            oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                            oSession.oResponse.headers["Date"] = DateTime.Now.ToString("R");
                            oSession.oResponse.headers["Server"] = "2chAPIProxy";
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=UTF-8";
                            oSession.utilSetResponseBody("<!DOCTYPE html><html><body>" + DateTime.Now.ToString("F") + "<br>" + oSession.clientIP + "を登録<br>2chAPIProxy再起動まで有効です</body></html>");
                            return true;
                        }
                    }
                    //未登録
                    ViewModel.OnModelNotice(oSession.clientIP + "から接続されました");
                    AuthIPList[oSession.clientIP] = new IPAuthData();
                    AuthIPList[oSession.clientIP].Auth = false;
                    AuthIPList[oSession.clientIP].nonce = System.Web.Security.Membership.GeneratePassword(24, 0);
                    oSession.utilCreateResponseAndBypassServer();
                    oSession.oResponse.headers.HTTPResponseCode = 401;
                    oSession.oResponse.headers.HTTPResponseStatus = "401 Authorization Required";
                    oSession.oResponse.headers["Date"] = DateTime.Now.ToString("R");
                    oSession.oResponse.headers["Server"] = "2chAPIProxy";
                    oSession.oResponse.headers["WWW-Authenticate"] = "Digest realm=" + '"' + "2chAPIProxy Auth" + '"' + ", nonce=" + '"' + AuthIPList[oSession.clientIP].nonce + '"' + ", algorithm=MD5, qop=" + '"' + "auth" + '"';
                    oSession.oResponse.headers["Connection"] = "close";
                    oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                    oSession.oResponse.headers["Cache-Control"] = "no-cache";
                    //AuthIP[oSession.clientIP] = false;
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                ViewModel.OnModelNotice(oSession.clientIP + "から接続されました");
                oSession.utilCreateResponseAndBypassServer();
                AuthIPList[oSession.clientIP] = new IPAuthData();
                AuthIPList[oSession.clientIP].Auth = false;
                AuthIPList[oSession.clientIP].nonce = System.Web.Security.Membership.GeneratePassword(24, 0);
                oSession.oResponse.headers.SetStatus(401, "401 Authorization Required"); oSession.oResponse.headers["Date"] = DateTime.Now.ToString("R");
                oSession.oResponse.headers["Server"] = "2chAPIProxy";
                oSession.oResponse.headers["WWW-Authenticate"] = "Digest realm=" + '"' + "2chAPIProxy Auth" + '"' + ", nonce=" + '"' + AuthIPList[oSession.clientIP].nonce + '"' + ", algorithm=MD5, qop=" + '"' + "auth" + '"';
                oSession.oResponse.headers["Connection"] = "close";
                oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                oSession.oResponse.headers["Cache-Control"] = "no-cache";
                //AuthIP[oSession.clientIP] = false;
                return false;
            }
        }

        private bool OtherLinkHTMLTrance(ref Session oSession)
        {
            try
            {
                oSession.fullUrl = oSession.fullUrl.Replace(".5ch.net/", ".2ch.net/");
                System.Threading.Thread HtmlTranceThread = null;
                bool offlowperm;
                String err = "";
                Byte[] Htmldat = null;

                if (OfflawRokkaPerm && CheckKakouri.IsMatch(oSession.fullUrl))
                {
                    //offlow2,rokkaへのアクセスをバイパスする
                    offlowperm = true;
                    oSession.utilCreateResponseAndBypassServer();
                    String URI = oSession.fullUrl;
                    String Host = oSession.oRequest.headers["Host"].Replace(".5ch.net", ".2ch.net");
                    String Referer = oSession.oRequest.headers["Referer"].Replace(".5ch.net", ".2ch.net"); ;
                    HtmlTranceThread = new System.Threading.Thread(() =>
                    {
                        String ThreadURI;
                        try
                        {
                            if (URI.Contains("offlaw2"))
                            {
                                ThreadURI = Referer;
                                if (ThreadURI.IndexOf(@"2ch.net/test/read.cgi/") < 0)
                                {
                                    String
                                        sever = Host,
                                        ita = Regex.Match(URI, @"&bbs=(.\w+?)&").Groups[1].Value,
                                        key = Regex.Match(URI, @"&key=(.\d+)").Groups[1].Value;
                                    ThreadURI = @"http://" + sever + @"/test/read.cgi/" + ita + @"/" + key + @"/";
                                }
                            }
                            else
                            {
                                err = "Success Archive\n";
                                var group = CheckKakouri.Match(URI).Groups;
                                ThreadURI = @"http://" + group[3].Value + "." + group[2].Value + @"/test/read.cgi/" + group[4].Value + @"/" + group[5].Value + @"/";
                            }
                            //Htmldat = HTMLtoDat.Gethtml(ThreadURI, -1, "", CRReplace);
                            Htmldat = HtmlConverter.Gethtml(ThreadURI, -1, "", CRReplace);
                        }
                        catch (System.Threading.ThreadAbortException)
                        {
                            ViewModel.OnModelNotice("タイムアウトによりHTML変換スレッドを中断。\nURI:" + URI);
                        }
                    });
                }
                else if (KakolinkPerm && !OnlyORPerm && CheckKakouri2.IsMatch(oSession.fullUrl))
                {
                    //kakoリンクのHTML変換応答置換
                    offlowperm = false;
                    oSession.utilCreateResponseAndBypassServer();
                    String URI = oSession.fullUrl;
                    HtmlTranceThread = new System.Threading.Thread(() =>
                    {
                        try
                        {
                            var group = CheckKakouri2.Match(URI).Groups;
                            String ThreadURI = "http://" + group[1].Value + "/test/read.cgi/" + group[2].Value + "/" + group[3].Value + "/";
                            //Htmldat = HTMLtoDat.Gethtml(ThreadURI, -1, "", CRReplace);
                            Htmldat = HtmlConverter.Gethtml(ThreadURI, -1, "", CRReplace);
                        }
                        catch (System.Threading.ThreadAbortException)
                        {
                            ViewModel.OnModelNotice("タイムアウトによりHTML変換スレッドを中断。\nURI:" + URI);
                        }
                    });
                }
                else return false;

                HtmlTranceThread.IsBackground = true;
                HtmlTranceThread.Start();
                if (HtmlTranceThread.Join(30 * 1000))
                {
                    if (offlowperm)
                    {
                        //offlaw2変換時
                        if (Htmldat.Length > 2)
                        {
                            ViewModel.OnModelNotice(oSession.fullUrl + " をhtmlから変換");
                            oSession.oResponse.headers.HTTPResponseCode = 200;
                            oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                            byte[] Res;
                            if (err != "") Res = Encoding.GetEncoding("shift_jis").GetBytes(err).Concat(Htmldat).ToArray();
                            else Res = Htmldat;
                            oSession.ResponseBody = Res;
                            if (ChunkRes) oSession.utilChunkResponse(3);
                        }
                        else
                        {
                            if (err.IndexOf("Success") > -1) err = "Error 13\n";
                            else err = "ERROR ret=2001 OL2ERROR##### dat()[.dat]";
                            oSession.oResponse.headers.SetStatus(302, "302 Found");
                            oSession.ResponseBody = Encoding.GetEncoding("shift_jis").GetBytes(err);
                        }
                    }
                    else
                    {
                        //kakoリンク変換時
                        if (Htmldat.Length > 2)
                        {
                            ViewModel.OnModelNotice(oSession.fullUrl + " をhtmlから変換");
                            oSession.oResponse.headers.SetStatus(200, "200 OK");
                            oSession.ResponseBody = Htmldat;
                        }
                        else oSession.oResponse.headers.SetStatus(302, "302 Found");
                    }
                    oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                    oSession.oResponse.headers["Server"] = "2chAPIProxy";
                    oSession.oResponse.headers["Vary"] = "Accept-Encoding";
                    oSession.oResponse.headers["Connection"] = "close";
                    oSession.oResponse.headers["Content-Type"] = "text/plain";
                    if (gZipRes) oSession.utilGZIPResponse();
                }
                else
                {
                    //変換が終わらなかった場合
                    HtmlTranceThread.Abort();
                    oSession.oResponse.headers.SetStatus(302, "302 Found");
                    oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                    oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                    oSession.oResponse.headers["Connection"] = "close";
                }
            }
            catch (Exception err)
            {
                oSession.oResponse.headers.SetStatus(302, "302 Found");
                oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                oSession.oResponse.headers["Connection"] = "close";
                ViewModel.OnModelNotice("offlow2/rokka/過去ログ倉庫へのアクセス置換部でエラーです。\n" + err.ToString());
            }
            return true;
        }

        void Be21Login(Session oSession, bool is2ch)
        {
            if (!BeLogin)
            {
                //Live2ch、Beログイン中のままになる対策
                if (oSession.oRequest.headers["User-Agent"].IndexOf("Live2ch") < 0)
                {
                    oSession.Ignore();
                    return;
                }
                SessionStateHandler BRHandler = null;
                BRHandler = (ooSession) =>
                {
                    //レスポンスヘッダにConnection:Closeを明示し、接続を切る
                    FiddlerApplication.BeforeResponse -= BRHandler;
                    ooSession.oResponse.headers["Connection"] = "Close";
                };
                FiddlerApplication.BeforeResponse += BRHandler;
                if (AllUAReplace) oSession.oRequest.headers["User-Agent"] = WriteUA;
                //レスポンス時に捕まえる必要があるため今は何もしない
                return;
            }
            //beの時、ログインセッション代行処理
            oSession.utilCreateResponseAndBypassServer();
            HttpWebRequest BeLoginReq = (HttpWebRequest)WebRequest.Create("https://be.5ch.net/log");
            BeLoginReq.Method = "POST";
            BeLoginReq.UserAgent = ViewModel.Setting.UserAgent4;
            if (Proxy != "") BeLoginReq.Proxy = new WebProxy(Proxy);
            BeLoginReq.Accept = "text/html";
            BeLoginReq.Referer = "https://be.5ch.net/";
            BeLoginReq.ContentType = "application/x-www-form-urlencoded";
            BeLoginReq.ServicePoint.Expect100Continue = false;
            BeLoginReq.CookieContainer = new CookieContainer();
            BeLoginReq.Host = "be.5ch.net";
            String reqdata = oSession.GetRequestBodyAsString();
            Byte[] PostData;
            if (Regex.IsMatch(reqdata, @"^mail=.+?@.+?&pass=.+?&login=$"))
            {
                PostData = oSession.requestBodyBytes;
            }
            else
            {
                String mail, pass;
                mail = Regex.Match(reqdata, @"(?:m|mail)=(.+?(?:@|%40).+?)(?:&|$)").Groups[1].Value;
                pass = Regex.Match(reqdata, @"(?:p|pass)=(.+?)(?:&|$)").Groups[1].Value;
                //var m = Regex.Match(reqdata, @"m=(.+?(?:@|%40).+?)&p=(.+?)(?:$|&.+$)").Groups;
                PostData = Encoding.ASCII.GetBytes("mail=" + mail + "&pass=" + pass + "&login=");
            }
            try
            {
                using (System.IO.Stream PostStream = BeLoginReq.GetRequestStream())
                {
                    PostStream.Write(PostData, 0, PostData.Length);
                    HttpWebResponse wres;
                    try
                    {
                        wres = (HttpWebResponse)BeLoginReq.GetResponse();
                    }
                    catch (WebException err)
                    {
                        wres = (HttpWebResponse)err.Response;
                    }
                    if (wres.Cookies.Count > 0)
                    {
                        var cul = new System.Globalization.CultureInfo("en-US");
                        string domain = (is2ch) ? (".2ch.net") : (".5ch.net");
                        foreach (Cookie cookie in wres.Cookies)
                        {
                            String tc = cookie.ToString();
                            tc += "; domain=" + domain;
                            if (cookie.Expires != null) tc += "; expires=" + cookie.Expires.ToUniversalTime().ToString("ddd, dd-MMM-yyyy HH:mm:ss", cul) + " GMT";
                            if (!String.IsNullOrEmpty(cookie.Path)) tc += "; path=" + cookie.Path;
                            oSession.oResponse.headers.Add("Set-Cookie", tc);
                        }
                    }
                    if ((int)wres.StatusCode == 200 && CheckOldBe.Match(oSession.fullUrl).Groups[1].Value == "index")
                    {
                        oSession.oResponse.headers.SetStatus(302, "Found");
                        oSession.oResponse.headers["Location"] = "http://be.2ch.net/status";
                    }
                    else oSession.oResponse.headers.SetStatus((int)wres.StatusCode, wres.StatusDescription);
                    oSession.oResponse.headers["Date"] = wres.Headers[HttpResponseHeader.Date];
                    oSession.oResponse.headers["Content-Type"] = wres.Headers[HttpResponseHeader.ContentType];
                    oSession.oResponse.headers["Connection"] = "close";
                    if (wres != null) wres.Close();
                    return;
                }
            }
            catch (Exception err)
            {
                ViewModel.OnModelNotice("Beログイン中にエラーが発生しました。\n" + err.ToString());
            }
        }

        private static void ShitarabaPost(Session oSession)
        {
            //したらば書き込み時
            if (!Regex.IsMatch(oSession.fullUrl, @"^https?://jbbs.(shitaraba.net|livedoor.jp)/bbs/write.cgi/\w+/\d+/\d+"))
            {
                //正規のURLでないとき
                try
                {
                    //書き込み置換処理、URL変更とデータの組直し
                    String oBody = HttpUtility.UrlDecode(oSession.GetRequestBodyAsString(), Encoding.GetEncoding("euc-jp")),
                    dir = Regex.Match(oSession.oRequest.headers["Referer"], @"^https?://jbbs.(?:shitaraba.net|livedoor.jp)/(\w+)/\d+").Groups[1].Value,
                    bbs = Regex.Match(oBody, @"BBS=(\d+)(?:&|$)", RegexOptions.IgnoreCase).Groups[1].Value,
                    key = Regex.Match(oBody, @"KEY=(\d+)(?:&|$)", RegexOptions.IgnoreCase).Groups[1].Value,
                    time = Regex.Match(oBody, @"TIME=(\d+)(?:&|$)", RegexOptions.IgnoreCase).Groups[1].Value,
                    name = HttpUtility.UrlEncode(Regex.Match(oBody, @"NAME=(.*?)(?:&\w+?=|$)", RegexOptions.IgnoreCase).Groups[1].Value, Encoding.GetEncoding("euc-jp")),
                    mail = HttpUtility.UrlEncode(Regex.Match(oBody, @"MAIL=(.*?)(?:&|$)", RegexOptions.IgnoreCase).Groups[1].Value, Encoding.GetEncoding("euc-jp")),
                    message = HttpUtility.UrlEncode(Regex.Match(oBody, @"MESSAGE=((?:.|\s)*?)(?:&\w+?=|$)", RegexOptions.IgnoreCase).Groups[1].Value, Encoding.GetEncoding("euc-jp"));
                    oSession.fullUrl = $"https://jbbs.shitaraba.net/bbs/write.cgi/{dir}/{bbs}/{key}/";
                    oSession.oRequest.headers["Content-Type"] = "application/x-www-form-urlencoded";
                    oSession.RequestBody = Encoding.ASCII.GetBytes("submit=%bd%f1%a4%ad%b9%fe%a4%e0&DIR=" + dir + "&BBS=" + bbs + "&KEY=" + key + "&TIME=" + time + "&MESSAGE=" + message + "&NAME=" + name + "&MAIL=" + mail);
                    oSession.oRequest.headers["Referer"] = oSession.oRequest.headers["Referer"].Replace("livedoor.jp", "shitaraba.net");
                }
                catch (NullReferenceException) { }
            }
            oSession.oRequest.headers.Remove("Pragma");
            oSession.oRequest.headers["Connection"] = "Keep-Alive";
            //BeforeResponseで応答を書き換えるために必須
            oSession.bBufferResponse = true;
            SessionStateHandler WRHandler = null;
            WRHandler = (ooSession) =>
            {
                //応答内容書き換え、句点を付ける
                FiddlerApplication.BeforeResponse -= WRHandler;
                ooSession.utilSetResponseBody(Regex.Replace(ooSession.GetResponseBodyAsString(), @"<title>書き(こ|込)み(まし|が完了しまし)た。?</title>", "<title>書きこみました。</title>"));
                ooSession.oResponse.headers["Connection"] = "Close";
            };
            FiddlerApplication.BeforeResponse += WRHandler;
            return;
        }

        private void ResPost(Session oSession, bool is2ch)
        {
            try
            {
                String ReqBody = oSession.GetRequestBodyAsString();
                //ギコナビ、レス投稿時にもsubject=が付いてる対策
                ReqBody = ReqBody.Replace("subject=&", "");
                //主にギコナビ、submitに改行が入っている
                ReqBody = ReqBody.Replace("\r\n", "");
                bool ResPost = !ReqBody.Contains("subject=");
                if (oSession.fullUrl.Contains("subbbs.cgi"))
                {
                    oSession.fullUrl = oSession.fullUrl.Replace("subbbs.cgi", "bbs.cgi");
                }
                String PostURI = (ViewModel.Setting.UseTLSWrite) ? (oSession.fullUrl.Replace("http://", "https://")) : (oSession.fullUrl);
                HttpWebRequest Write = (HttpWebRequest)WebRequest.Create(PostURI);
                Write.Method = "POST";
                Write.ServicePoint.Expect100Continue = false;
                Write.Headers.Clear();
                //ここで指定しないとデコードされない
                Write.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                //デバッグ出力
                System.Diagnostics.Debug.WriteLine("オリジナルリクエストヘッダ");
                foreach (var header in oSession.RequestHeaders)
                {
                    System.Diagnostics.Debug.WriteLine($"{header.Name}:{header.Value}");
                }

                if (this.WriteHeader.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("Styleヘッダを設定");

                    Write.ProtocolVersion = HttpVersion.Version10;
                    Write.UserAgent = (String.IsNullOrEmpty(WriteUA)) ? ("Monazilla/1.00 JaneStyle/4.00 Windows/6.1.7601 Service Pack 1") : (WriteUA);
                    Write.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                    Write.Headers.Add("Accept-Encoding", "gzip, identity");
                    Write.ContentType = "application/x-www-form-urlencoded";
                    Write.KeepAlive = false;
                    //Write.ProtocolVersion = HttpVersion.Version10;
                    //Write.UserAgent = "Monazilla/1.00 Live5ch/1.52 Windows/6.1.7601 (Service Pack 1)";
                    //Write.Accept = "text/plain";
                    //Write.Headers.Add("Accept-Encoding", "");
                    //Write.ContentType = "application/x-www-form-urlencoded";
                    //Write.Headers.Add("Accept-charset", "shift_jis");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("外部定義ヘッダを使用して書き込み");

                    if (WriteHeader.ContainsKey("Accept") == true)
                    {
                        Write.Accept = WriteHeader["Accept"];
                    }
                    if (WriteHeader.ContainsKey("User-Agent") == true)
                    {
                        Write.UserAgent = WriteHeader["User-Agent"];
                    }
                    else
                    {
                        Write.UserAgent = (String.IsNullOrEmpty(WriteUA) == true) ? (oSession.oRequest.headers["User-Agent"]) : (WriteUA);
                    }
                    if (WriteHeader.ContainsKey("Expect") == true)
                    {
                        Write.Expect = WriteHeader["Expect"];
                    }
                    if (WriteHeader.ContainsKey("Content-Type") == true)
                    {
                        Write.ContentType = WriteHeader["Content-Type"];
                    }
                    if (WriteHeader.ContainsKey("Connection ") == true)
                    {
                        Write.KeepAlive = true;
                        Write.Connection = WriteHeader["Connection "];
                    }

                    foreach (var header in WriteHeader)
                    {
                        try
                        {
                            if (Regex.IsMatch(header.Key, @"(^HTTPVer$|^Accept$|^User-Agent$|^Expect$|^Content-Type$|^Connection$|^Cookie$)") == true) continue;
                            Write.Headers.Add(header.Key, header.Value);
                        }
                        catch (Exception err)
                        {
                            System.Diagnostics.Debug.WriteLine("●ヘッダ定義の適用中のエラー\n" + err.ToString());
                        }
                    }

                    if (WriteHeader.ContainsKey("HTTPVer") == true)
                    {
                        if (WriteHeader["HTTPVer"] == "1.0")
                        {
                            Write.ProtocolVersion = HttpVersion.Version10;
                        }
                        else
                        {
                            Write.ProtocolVersion = HttpVersion.Version11;
                        }
                    }
                }
                String referer = oSession.oRequest.headers["Referer"].Replace("2ch.net", "5ch.net");
                Write.Referer = referer.Replace("http:", "https:");

                if (Proxy != "") Write.Proxy = new WebProxy(Proxy);
                Write.CookieContainer = new CookieContainer();
                //送信されてきたクッキーを抽出
                foreach (Match mc in Regex.Matches(oSession.oRequest.headers["Cookie"], @"(?:\s+|^)((.+?)=(?:|.+?)(?:;|$))"))
                {
                    Cookie[mc.Groups[2].Value] = mc.Groups[1].Value;
                }
                Cookie.Remove("sid");
                Cookie.Remove("SID");
                //送信クッキーのセット
                String domain = CheckWriteuri.Match(oSession.fullUrl).Groups[1].Value;
                //String domain = ".5ch.net";
                foreach (var cook in Cookie)
                {
                    if (cook.Value != "")
                    {
                        var m = Regex.Match(cook.Value, @"^(.+?)=(.*?)(;|$)");
                        try
                        {
                            Write.CookieContainer.Add(new Cookie(m.Groups[1].Value, m.Groups[2].Value, "/", domain));
                        }
                        catch (CookieException)
                        {
                            continue;
                        }
                        //if (cook.Key == "PREN" || cook.Key == "yuki" || cook.Key == "MDMD" || cook.Key == "DMDM")
                    }
                }
                //浪人を無効化
                if (ViewModel.Setting.PostRoninInvalid && ReqBody.Contains("sid="))
                {
                    ReqBody = Regex.Replace(ReqBody, @"sid=.+?(?:&|$)", "");
                    ReqBody = Regex.Replace(ReqBody, @"&$", "");
                }
                //お絵かき用のデータ追加
                if (ResPost && !ReqBody.Contains("&oekaki_thread") && !oSession.host.Contains("qb5.5ch.net"))
                {
                    ReqBody = ReqBody.Replace("\r\n", "");
                    ReqBody += "&oekaki_thread1=";
                }
                Byte[] Body = Encoding.GetEncoding("Shift_JIS").GetBytes(ReqBody);
                Write.ContentLength = Body.Length;
                try
                {
                    using (System.IO.Stream PostStream = Write.GetRequestStream())
                    {
                        PostStream.Write(Body, 0, Body.Length);
                        foreach(var header in Write.Headers.AllKeys)
                        {
                            System.Diagnostics.Debug.WriteLine($"{header}:{Write.Headers[header].ToString()}");
                        }

                        HttpWebResponse wres = (HttpWebResponse)Write.GetResponse();
                        if (wres.Cookies.Count > 0)
                        {
                            var cul = new System.Globalization.CultureInfo("en-US");
                            foreach (System.Net.Cookie cookie in wres.Cookies)
                            {
                                String tc = Cookie[cookie.Name] = cookie.ToString();
                                if (cookie.Expires != null) tc += "; expires=" + cookie.Expires.ToUniversalTime().ToString("ddd, dd-MMM-yyyy HH:mm:ss", cul) + " GMT";
                                if (!String.IsNullOrEmpty(cookie.Path)) tc += "; path=" + cookie.Path;
                                if (!String.IsNullOrEmpty(cookie.Domain)) tc += "; domain=" + ((is2ch) ? (cookie.Domain.Replace("5ch.net", "2ch.net")) : (cookie.Domain));
                                oSession.oResponse.headers.Add("Set-Cookie", tc);
                            }
                        }
                        Cookie["DMDM"] = Cookie["MDMD"] = "";
                        using (System.IO.StreamReader Res = new System.IO.StreamReader(wres.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                        {
                            oSession.oResponse.headers.HTTPResponseCode = (int)wres.StatusCode;
                            oSession.oResponse.headers.HTTPResponseStatus = (int)wres.StatusCode + " " + wres.StatusDescription;
                            if(oSession.oRequest.headers["User-Agent"].Contains("Live2ch")) oSession.oResponse.headers["Connection"] = "Close";
                            else oSession.oResponse.headers["Connection"] = "keep-alive";
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=Shift_JIS";
                            oSession.oResponse.headers["Date"] = wres.Headers[HttpResponseHeader.Date];
                            oSession.oResponse.headers["Vary"] = "Accept-Encoding";
                            String resdat = Res.ReadToEnd();
                            oSession.utilSetResponseBody(resdat);
                            //oSession.utilSetResponseBody(Res.ReadToEnd());
                            //if (gZipRes) oSession.utilGZIPResponse();
                        }

                        System.Diagnostics.Debug.WriteLine("レスポンスヘッダ");
                        foreach (var header in Write.Headers.AllKeys)
                        {
                            System.Diagnostics.Debug.WriteLine($"{header}:{Write.Headers[header].ToString()}");
                        }

                        System.Diagnostics.Debug.WriteLine("レスポンスヘッダ");
                        foreach (var header in wres.Headers.AllKeys)
                        {
                            System.Diagnostics.Debug.WriteLine($"{header}:{wres.Headers[header].ToString()}");
                        }

                        if (wres != null) wres.Close();
                        return;
                    }
                }
                catch (WebException err)
                {
                    Cookie["DMDM"] = Cookie["MDMD"] = "";
                    ViewModel.OnModelNotice("書き込み中にエラーが発生しました。\n" + err.ToString());
                    oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                    return;
                }
                catch (NullReferenceException err)
                {
                    Cookie["DMDM"] = Cookie["MDMD"] = "";
                    ViewModel.OnModelNotice("書き込み中にエラーが発生しました。\n" + err.ToString());
                    oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                    return;
                }
            }
            catch (Exception err)
            {
                Cookie["DMDM"] = Cookie["MDMD"] = "";
                oSession.oResponse.headers.SetStatus(404, "404 NotFound");
                oSession.oResponse.headers["Content-Type"] = "text/html; charset=Shift_JIS";
                oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                oSession.oResponse.headers["Connection"] = "Close";
                oSession.utilSetResponseBody("2chAPIProxy書き込み処理中にエラーが発生しました。\n" + err.ToString());
                ViewModel.OnModelNotice("書き込み部でエラーです。\n" + err.ToString());
            }
            return;
        }

        private void GetDat(ref Session oSession, bool is2ch)
        {
            try
            {
                int range;
                bool retry = true, retrydat = true;
                HttpWebResponse dat;
                String last = oSession.oRequest.headers["If-Modified-Since"], hrange = oSession.oRequest.headers["Range"];
                range = (!String.IsNullOrEmpty(hrange)) ? (int.Parse(Regex.Match(hrange, @"\d+").Value)) : (-1);
                if (String.IsNullOrEmpty(last)) last = "1970/12/1";
                //スレッドステータス
                int Status = 0;

                Match ch2uri = CheckDaturi.Match(oSession.fullUrl);
                datget:
                try
                {
                    dat = APIMediator.GetDat(ch2uri.Groups[1].Value, ch2uri.Groups[3].Value, ch2uri.Groups[4].Value, range, last);
                }
                catch (Exception err)
                {
                    if (retrydat)
                    {
                        retrydat = false;
                        goto datget;
                    }
                    else
                    {
                        ViewModel.OnModelNotice("datアクセス中にエラーが発生しました。\n" + err.ToString());
                        oSession.oResponse.headers.HTTPResponseCode = 304;
                        oSession.oResponse.headers.HTTPResponseStatus = "304 Not Modified";
                        oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                        oSession.oResponse.headers["Connection"] = "close";
                        return;
                    }
                }
                //bool bat = CheckAlive(@"http://itest.2ch.net/public/newapi/client.php?subdomain=" + ch2uri.Groups[1].Value + "&board=" + ch2uri.Groups[3].Value + "&dat=" + ch2uri.Groups[4].Value);
                //ViewModel.OnModelNotice("生存判定：" + bat);

                if (dat == null)
                {
                    ViewModel.OnModelNotice("datの取得に失敗しました。");
                    if (oSession.oRequest.headers["User-Agent"].Contains("Jane"))
                    {
                        oSession.oResponse.headers.SetStatus(504, "504 Gateway Timeout");
                    }
                    else
                    {
                        oSession.oResponse.headers.SetStatus(304, "304 Not Modified");
                    }
                    oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                    oSession.oResponse.headers["Connection"] = "close";

                    return;
                }

                switch (dat.StatusCode)
                {
                    case HttpStatusCode.PartialContent:
                        oSession.oResponse.headers.HTTPResponseCode = 206;
                        oSession.oResponse.headers.HTTPResponseStatus = "206 Partial Content";
                        oSession.oResponse.headers["Content-Type"] = "text/plain";
                        oSession.oResponse.headers["Last-Modified"] = dat.Headers[HttpResponseHeader.LastModified];
                        oSession.oResponse.headers["Accept-Ranges"] = "bytes";
                        oSession.oResponse.headers["Content-Range"] = dat.Headers[HttpResponseHeader.ContentRange];
                        oSession.oResponse.headers["ETag"] = dat.Headers[HttpResponseHeader.ETag];
                        using (System.IO.StreamReader reader = new System.IO.StreamReader(dat.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                        //using (System.IO.BinaryReader res = new System.IO.BinaryReader(dat.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                        {
                            //oSession.ResponseBody = res.ReadBytes(50 * 1024 * 1024);
                            String resdat = reader.ReadToEnd();
                            if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink)
                            {
                                //resdat = HTMLtoDat.ResContentReplace(resdat);
                                resdat = HtmlConverter.ResContentReplace(resdat);
                            }
                            oSession.ResponseBody = Encoding.GetEncoding("Shift_JIS").GetBytes(resdat);
                        }
                        break;
                    case HttpStatusCode.NotModified:
                        oSession.oResponse.headers.HTTPResponseCode = 304;
                        oSession.oResponse.headers.HTTPResponseStatus = "304 Not Modified";
                        oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                        oSession.oResponse.headers["ETag"] = dat.Headers[HttpResponseHeader.ETag];
                        break;
                    case HttpStatusCode.OK:
                        //goto case HttpStatusCode.InternalServerError;
                        //Thread-Statusチェック
                        try
                        {
                            String th = dat.Headers["Thread-Status"];
                            if (!String.IsNullOrEmpty(th)) Status = int.Parse(th);
                            else Status = 1;
                        }
                        catch (FormatException)
                        {
                            Status = 1;
                        }
                        if (Status >= 2) goto case HttpStatusCode.NotImplemented;
                        using (System.IO.StreamReader reader = new System.IO.StreamReader(dat.GetResponseStream(), Encoding.GetEncoding("Shift_JIS")))
                        {
                            String res1 = reader.ReadLine();
                            if (dat.ContentLength > 0 && dat.ContentLength < 26)
                            {
                                //res = reader.ReadToEnd();
                                if (Regex.IsMatch(res1, @"ng \(([a-z]\s?)+\)"))
                                {
                                    ViewModel.OnModelNotice("SessionIDがおかしいようです。各keyを確認の上再取得してください。\n" + res1, false);
                                    goto case HttpStatusCode.NotModified;
                                }
                            }
                            if (CRReplace)
                            {
                                try
                                {
                                    //res1 = reader.ReadLine();
                                    String title = Regex.Match(res1, @"<>.*?<>.+?<>.+?<>(.+?&#169;.+?)$").Groups[1].Value;
                                    if (!String.IsNullOrEmpty(title))
                                    {
                                        String ntitle = title.Replace("&#169;", "&copy;");
                                        res1 = res1.Replace(title, ntitle);
                                        //ret = Encoding.GetEncoding("Shift_JIS").GetBytes(res1 + "\n" + reader.ReadToEnd());
                                    }
                                }
                                catch (Exception) { }
                            }
                            String resdat = res1 + "\n" + reader.ReadToEnd();
                            if (ViewModel.Setting.Replace5chURI || ViewModel.Setting.ReplaceHttpsLink)
                            {
                                //resdat = HTMLtoDat.ResContentReplace(resdat);
                                resdat = HtmlConverter.ResContentReplace(resdat);
                            }
                            oSession.ResponseBody = Encoding.GetEncoding("Shift_JIS").GetBytes(resdat);
                        }
                        oSession.oResponse.headers.HTTPResponseCode = 200;
                        oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                        oSession.oResponse.headers["Content-Type"] = "text/plain";
                        oSession.oResponse.headers["Last-Modified"] = dat.Headers[HttpResponseHeader.LastModified];
                        oSession.oResponse.headers["ETag"] = dat.Headers[HttpResponseHeader.ETag];
                        if (gZipRes) oSession.utilGZIPResponse();
                        break;
                    case HttpStatusCode.NotImplemented:
                        if (!GetHTML || OnlyORPerm)
                        {
                            oSession.oResponse.headers.HTTPResponseCode = 302;
                            oSession.oResponse.headers.HTTPResponseStatus = "302 Found";
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                            break;
                        }
                        goto case HttpStatusCode.InternalServerError;
                    case HttpStatusCode.Unauthorized:
                        lock (new object())
                        {
                            if (!retry || SIDNowUpdate)
                            {
                                if (SIDNowUpdate) ViewModel.OnModelNotice("403応答によるSessionID更新を10秒間停止中です、しばらくお待ちください。");
                                goto case HttpStatusCode.NotModified;
                            }
                            SIDNowUpdate = true;
                        }
                        try
                        {
                            APIMediator.UpdateSID();
                            ViewModel.OnModelNotice("SessionIDを更新しました。（期限切れ）");
                            //if (!APIMediator.GetSIDFailed) ViewModel.OnModelNotice("SessionIDを更新しました。（期限切れ）");
                            //APIMediator.GetSIDFailed = false;
                        }
                        catch (Exception err)
                        {
                            ViewModel.OnModelNotice("SessionIDの更新に失敗しました\n" + err.ToString());
                        }
                        dat.Close();
                        retry = false;
                        //403応答によるSID更新を10秒間ブロックする
                        System.Threading.Timer ReleaseSIDUpdate = null;
                        ReleaseSIDUpdate = new System.Threading.Timer((e) =>
                        {
                            using (ReleaseSIDUpdate)
                            {
                                SIDNowUpdate = false;
                            }
                        }, null, 10000, System.Threading.Timeout.Infinite);
                        goto datget;
                    case HttpStatusCode.InternalServerError:
                        Byte[] Htmldat = null;
                        String uri = @"http://" + ch2uri.Groups[1].Value + "." + ch2uri.Groups[2].Value + "/test/read.cgi/" + ch2uri.Groups[3].Value + @"/" + ch2uri.Groups[4].Value + @"/";
                        if (GetHTML && !OnlyORPerm)
                        {
                            String UA = oSession.oRequest.headers["User-Agent"];
                            System.Threading.Thread HtmlTranceThread = new System.Threading.Thread(() =>
                            {
                                try
                                {
                                    //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                                    //sw.Start();
                                    //Htmldat = HTMLtoDat.Gethtml(uri, range, UA, CRReplace, (last != "1970/12/1") ? (last) : (null));
                                    Htmldat = HtmlConverter.Gethtml(uri, range, UA, CRReplace, (last != "1970/12/1") ? (last) : (null));
                                    //sw.Stop();
                                    //System.Diagnostics.Debug.WriteLine("処理時間：" + sw.ElapsedMilliseconds + "ms");
                                }
                                catch (System.Threading.ThreadAbortException)
                                {
                                    ViewModel.OnModelNotice("タイムアウトによりHTML変換スレッドを中断。\nURI:" + uri);
                                }
                            });
                            HtmlTranceThread.IsBackground = true;
                            HtmlTranceThread.Start();
                            if (!HtmlTranceThread.Join(30 * 1000))
                            {
                                //変換が終わらなかった場合
                                HtmlTranceThread.Abort();
                                Htmldat = new byte[] { 0 };
                            }
                        }
                        else
                        {
                            if (CheckAlive(@"http://itest.2ch.net/public/newapi/client.php?subdomain=" + ch2uri.Groups[1].Value + "&board=" + ch2uri.Groups[3].Value + "&dat=" + ch2uri.Groups[4].Value)) Htmldat = new byte[] { 0, 0 };
                            else Htmldat = new byte[] { 0 };
                        }
                        if (Htmldat.Length == 2 && Status < 2) goto case HttpStatusCode.NotModified;
                        if (Htmldat.Length == 1 || (Htmldat.Length == 2 && Status >= 2))
                        {
                            oSession.oResponse.headers.SetStatus(302, "302 Found");
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                            break;
                        }
                        ViewModel.OnModelNotice(uri + " をhtmlから変換");
                        if (!ViewModel.Setting.AllReturn && range > 0)
                        {
                            oSession.oResponse.headers.HTTPResponseCode = 206;
                            oSession.oResponse.headers.HTTPResponseStatus = "206 Partial Content";
                            oSession.oResponse.headers["Accept-Ranges"] = "bytes";
                            oSession.oResponse.headers["Content-Range"] = "bytes " + range + "-" + (range + Htmldat.Length - 1) + "/" + (range + Htmldat.Length);
                        }
                        else
                        {
                            oSession.oResponse.headers.HTTPResponseCode = 200;
                            oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                        }
                        oSession.oResponse.headers["Last-Modified"] = DateTime.Now.ToUniversalTime().ToString("R");
                        oSession.oResponse.headers["Content-Type"] = "text/plain";
                        oSession.ResponseBody = Htmldat;
                        break;
                    case HttpStatusCode.BadGateway:
                        goto case HttpStatusCode.NotModified;
                    case HttpStatusCode.Found:
                        goto case HttpStatusCode.NotImplemented;
                    case HttpStatusCode.NotFound:
                        //if (CheckAlive(@"http://" + ch2uri.Groups[1].Value + "." + ch2uri.Groups[2].Value + "/test/read.cgi/" + ch2uri.Groups[3].Value + @"/" + ch2uri.Groups[4].Value + @"/"))
                        if (CheckAlive(@"http://itest.2ch.net/public/newapi/client.php?subdomain=" + ch2uri.Groups[1].Value + "&board=" + ch2uri.Groups[3].Value + "&dat=" + ch2uri.Groups[4].Value))
                        {
                            oSession.oResponse.headers.HTTPResponseCode = 416;
                            oSession.oResponse.headers.HTTPResponseStatus = "416 Requested range not satisfiable";
                            oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                            break;
                        }
                        else goto case HttpStatusCode.NotImplemented;
                    default:
                        oSession.oResponse.headers.HTTPResponseCode = (int)dat.StatusCode;
                        oSession.oResponse.headers.HTTPResponseStatus = (int)dat.StatusCode + " " + dat.StatusDescription;
                        oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                        break;
                }
                oSession.oResponse.headers["Date"] = dat.Headers[HttpResponseHeader.Date];
                oSession.oResponse.headers["Set-Cookie"] = (is2ch) ? (dat.Headers[HttpResponseHeader.SetCookie].Replace("5ch.net", "2ch.net")) : (dat.Headers[HttpResponseHeader.SetCookie]);
                oSession.oResponse.headers["Connection"] = "close";
                dat.Close();
            }
            catch (Exception err)
            {
                oSession.oResponse.headers.SetStatus(304, "304 Not Modified");
                oSession.oResponse.headers["Content-Type"] = "text/html; charset=iso-8859-1";
                oSession.oResponse.headers["Date"] = DateTime.Now.ToUniversalTime().ToString("R");
                oSession.oResponse.headers["Connection"] = "close";
                ViewModel.OnModelNotice("datアクセス部でエラーです。\n" + err.ToString());
            }
            return;
        }

        String CMD5(String value)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                Byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        bool CheckAlive(String URI)
        {
            try
            {
                //if (ViewModel.Setting.Use5chnet) URI.Replace("2ch.net", "5ch.net");
                URI = URI.Replace("2ch.net", "5ch.net");
                using (WebClient get = new WebClient())
                {
                    get.Headers["User-Agent"] = HtmlConverter.UserAgent;
                    if (Proxy != "") get.Proxy = new WebProxy(Proxy);
                    using (System.IO.StreamReader html = new System.IO.StreamReader(get.OpenRead(URI), Encoding.GetEncoding("Shift_JIS")))
                    {
                        if (html.EndOfStream) return false;
                        else return true;
                        //for (int i = 0; i < 40 && !html.EndOfStream; ++i)
                        //{
                        //    String res = html.ReadLine();
                        //    //if (res.IndexOf(">■ このスレッドは過去ログ倉庫に格納されています<") >= 0) return false;
                        //    if (Regex.IsMatch(res, @"<div\s.+?>.*?(過去ログ倉庫に格納されています|レス数が1000を超えています).*?<\/div>")) return false;
                        //    if (Regex.IsMatch(res, @"(２ちゃんねる error \d+|(.+)?datが存在しません.削除されたかURL間違ってますよ)")) return false;
                        //}
                        //return true;
                    }
                }
            }
            catch (WebException)
            {
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public int Start(int PortNum)
        {
            FiddlerCoreStartupFlags f = (AllowWANAccese) ? (FiddlerCoreStartupFlags.AllowRemoteClients | FiddlerCoreStartupFlags.OptimizeThreadPool) : (FiddlerCoreStartupFlags.OptimizeThreadPool);
            FiddlerApplication.Startup(PortNum, f);
            return FiddlerApplication.oProxy.ListenPort;
        }

        public void End()
        {
            FiddlerApplication.Shutdown();
        }

        public Task UpdateAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                APIMediator.UpdateSID();
            });
        }
    }
}

//http://www2.hatenadiary.jp/entry/2013/12/11/215927
//1050まであるスレ
//http://news.2ch.net/test/read.cgi/newsplus/1023016978/