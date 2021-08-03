using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace RiseKeyHunter
{
    class Program
    {
        private CookieContainer cookies = new CookieContainer();
        private List<ulong> channelIDS = new List<ulong>();
        private readonly DiscordSocketClient _client;
        private bool useTesseract = true;

        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public Program()
        {
            _client = new DiscordSocketClient();

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedAsync;
        }

        public async Task MainAsync()
        {
            if (!Directory.Exists(@"C:\Program Files\Tesseract-OCR"))
            {
                Console.WriteLine("Tesseract bulunamadı resimden okuma yapılamayacak, devam etmek ister misiniz?\nEvet için Y\nHayır için N");
                string option = Console.ReadLine();
                if (option.Equals("Y", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.useTesseract = false;
                }
                else
                {
                    return;
                }
            }
            Console.Write("Discord hesabınızın tokenini giriniz : ");
            string usertoken = Console.ReadLine();
            if (usertoken.StartsWith("mfa"))
            {
                Console.WriteLine("2 Faktöryeli açık olan hesaplarda selfbot çalışmaz.");
                Console.WriteLine("Desteklenen token formatları : \"O\", \"N\" harfleriyle başlayan tokenlerdir.");
                Console.ReadLine();
                return;
            }
            try
            {
                await _client.LoginAsync(0, usertoken);
            }
            catch (HttpException e)
            {
                if (e.Message.Contains("Unauthorized"))
                {
                    Console.WriteLine("Hatalı bir token girildi.");
                    Console.ReadLine();
                    return;
                }
                else
                {
                    throw;
                }
            }
            string content = getToken();
            Regex find = new Regex(@"\(('[a-z0-9_\-]+')\)");
            string token = find.Match(content).ToString().Replace("')", "").Replace("('", "");
            Console.Write("Craftrise kullanıcı isminizi giriniz : ");
            string username = Console.ReadLine();
            Console.Write("Craftrise şifrenizi giriniz : ");
            string password = Console.ReadLine();
            if (this.login(username, password, token))
            {
                Console.WriteLine("Craftriseye giriş başarılı işleme hazırsınız!");
            }
            else
            {
                Console.WriteLine("Craftriseye girerken bir hata oluştu, kullanıcı isminizi ve şifrenizi kontrol ediniz.");
                Console.ReadLine();
                return;
            }
            Console.Write("Kaç tane kanalı izliceksiniz : ");
            int count = -1;
            try
            {
                count = int.Parse(Console.ReadLine());
                if (count <= 0)
                {
                    Console.WriteLine("Girilen sayı 0 dan büyük olmalı.");
                    Console.ReadLine();
                    return;
                }
            }
            catch (FormatException)
            {
                Console.WriteLine("Formatta sıkıntı var.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Kanal ID'lerini yazınız : ");

            for (int i = 0; i < count; ++i)
            {
                try
                {
                    channelIDS.Add(ulong.Parse(Console.ReadLine()));
                }
                catch (FormatException)
                {
                    Console.WriteLine("Formatta sıkıntı var.");
                    Console.ReadLine();
                    return;
                }
            }


            Console.WriteLine("Her şey hazır işleme hazırsınız!");

            await _client.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }

        private Task LogAsync(LogMessage log)
        {
            //Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            return Task.CompletedTask;
        }

        public static long getTimeStamp()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {
            bool check = false;

            foreach (var id in channelIDS)
            {
                if (message.Channel.Id == id)
                {
                    check = true;
                    break;
                }
            }

            if (!check)
                return;

            if (message.Author.Id == _client.CurrentUser.Id)
                return;


            string content = "empty";

            if (message.Attachments.Count > 0 && this.useTesseract)
            {
                var enumurator = message.Attachments.GetEnumerator();
                enumurator.MoveNext();
                string url = enumurator.Current.Url;
                byte[] data = new WebClient().DownloadData(url);

                File.WriteAllBytes("image.png", data);

                Pix pix = Pix.LoadFromFile("image.png");

                var ocr = new TesseractEngine("C:/Program Files/Tesseract-OCR/tessdata", "eng", EngineMode.Default);
                Page page = ocr.Process(pix, PageSegMode.Auto);

                content = page.GetText();
            }
            else
            {
                content = message.Content;
            }

            if (content.Equals("empty"))
            {
                return;
            }

            foreach (string line in content.Split('\n'))
            {
                List<Regex> regexList = new List<Regex>();

                regexList.Add(new Regex(@"RISE-[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}-\b[0-9A-Z]{4}"));
                regexList.Add(new Regex(@"RISEKINGDOMS-[0-9A-Z]{3}-[0-9A-Z]{4}-[0-9A-Z]{3}"));
                foreach (var regex in regexList)
                {
                    foreach (var match in regex.Matches(line))
                    {
                        object[] result = this.useCode(match.ToString());
                        if (result[0].ToString().Contains("True") || result[0].ToString().Contains("true"))
                        {
                            Console.WriteLine("Kod başarıyla kullanılmıştır, yeni krediniz : " + result[1]);
                        }
                        else
                        {
                            Console.WriteLine("Bu kod hatalı veya kullanılmış : " + result[2]);
                        }
                    }
                }
            }
        }

        public string getToken()
        {
            var request = (HttpWebRequest)WebRequest.Create("https://www.craftrise.com.tr/");

            request.Method = "GET";
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0";
            request.Host = "www.craftrise.com.tr";
            request.CookieContainer = this.cookies;

            return new StreamReader(request.GetResponse().GetResponseStream()).ReadToEnd();
        }

        public bool login(String username, String password, String token)
        {
            string post = "value=" + username + "&password=" + password + "&token=" + token;

            byte[] postBytes = Encoding.UTF8.GetBytes(post);

            var request = (HttpWebRequest)WebRequest.Create("https://www.craftrise.com.tr/posts/post-login.php");

            request.Method = "POST";
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0";
            request.Host = "www.craftrise.com.tr";
            request.CookieContainer = this.cookies;

            request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            request.ContentLength = postBytes.Length;

            request.GetRequestStream().Write(postBytes, 0, postBytes.Length);

            string response = new StreamReader(request.GetResponse().GetResponseStream()).ReadToEnd();

            JObject json = JObject.Parse(response);

            return json["resultType"].ToString().Equals("success");
        }

        public object[] useCode(String code)
        {
            string post = "code=" + code;

            byte[] postBytes = Encoding.UTF8.GetBytes(post);

            var request = (HttpWebRequest)WebRequest.Create("https://www.craftrise.com.tr/posts/post-code.php");

            request.Method = "POST";
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:90.0) Gecko/20100101 Firefox/90.0";
            request.Host = "www.craftrise.com.tr";
            request.CookieContainer = this.cookies;

            request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            request.ContentLength = postBytes.Length;

            request.GetRequestStream().Write(postBytes, 0, postBytes.Length);

            string response = new StreamReader(request.GetResponse().GetResponseStream()).ReadToEnd();

            JObject json = JObject.Parse(response);

            return new object[] { json["resultMessage"].ToString().Contains("Tebrikler"), json["credits"].ToString(), code };
        }
    }
}
