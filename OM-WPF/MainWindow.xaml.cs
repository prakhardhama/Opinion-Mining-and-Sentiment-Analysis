using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HtmlAgilityPack;
using opennlp.tools.postag;
using opennlp.tools.tokenize;
using java.io;

namespace OM_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        Debug debug = new Debug();
        
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            NextReview.Visibility = Visibility.Hidden;
            PreviousReview.Visibility = Visibility.Hidden;
            debug.Show();
        }

        private bool _hasValidURI;

        public bool HasValidURI
        {
            get { return _hasValidURI; }
            set { _hasValidURI = value; OnPropertyChanged("HasValidURI"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }

        private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Uri uri;
            HasValidURI = Uri.TryCreate((sender as TextBox).Text, UriKind.Absolute, out uri);
        }

        private void UrlTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Uri uri;
            UrlTextBox.SelectAll();
            if (Uri.TryCreate((sender as TextBox).Text, UriKind.Absolute, out uri))
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
            }
        }

        private void ScrapeButton_Click(object sender, RoutedEventArgs e)
        {
            debug.Clear();
            if (HasValidURI)
                ScrapePage(UrlTextBox.Text);
            else
            {
                author = new List<String>();heading = new List<String>();comments = new List<String>();
                comments.Add(UrlTextBox.Text);
                author.Add("Manual Entry");
                heading.Add("");
            }
            Program();
            NextReview.Visibility = Visibility.Visible;
            PreviousReview.Visibility = Visibility.Visible;
            RatingGrid.Visibility = Visibility.Visible;
        }

        int reviewNo = 0;
        private void NextReview_Click(object sender, RoutedEventArgs e)
        {
            if (reviewNo + 1 < comments.Count)
            {
                ++reviewNo;
                Program(reviewNo);
            }
        }

        private void PreviousReview_Click(object sender, RoutedEventArgs e)
        {
            if (reviewNo - 1 >= 0)
            {
                --reviewNo;
                Program(reviewNo);
            }
        }


        //Main Program
        List<string> author,heading,comments;
        List<int> grade;

        string modelPath = "D:\\Visual Studio\\Library\\Models\\";
        string[] Tokenizer(string review)
        {
            InputStream modelIn = new FileInputStream(modelPath + "en-token.zip");
            TokenizerModel model = new TokenizerModel(modelIn);
            TokenizerME tokenizer = new TokenizerME(model);
            string[] tokens = tokenizer.tokenize(review.Replace(".", ""));
            return tokens;
        }

        string[] POSTagger(string[] tokens)
        {
            InputStream modelIn = new FileInputStream(modelPath + "en-pos-maxent.zip");
            POSModel model = new POSModel(modelIn);
            POSTaggerME tagger = new POSTaggerME(model);
            string[] tags = tagger.tag(tokens);
            //int i = 0;
            //foreach (string s in tags)
            //{
            //    System.Console.WriteLine("{0} : {1}", tokens[i], s);
            //    debug.Print(tokens[i] + " : " + s + "\n");
            //    i++;
            //}
            return tags;
        }

        List<string> ExtractPhrases(string[] tags, string[] tokens)
        {
            List<string> phrases = new List<string>();
            for (int i = 0; i < tags.Length; ++i)
            {
                if (tags[i].Equals("JJ"))
                {
                    if ((i + 1) < tags.Length && (tags[i + 1].Equals("NN") || tags[i + 1].Equals("NNS")))
                        phrases.Add(tokens[i] + " " + tokens[i + 1]);
                    else if ((i + 1) < tags.Length && tags[i + 1].Equals("JJ"))
                        if ((i + 2) >= tags.Length || !(tags[i + 2].Equals("NN") || tags[i + 2].Equals("NNS")))
                            phrases.Add(tokens[i] + " " + tokens[i + 1]);
                }
                else if (tags[i].Equals("NN") || tags[i].Equals("NNS"))
                {
                    if ((i + 1) < tags.Length && tags[i + 1].Equals("JJ"))
                        if ((i + 2) >= tags.Length || !(tags[i + 2].Equals("NN") || tags[i + 2].Equals("NNS")))
                            phrases.Add(tokens[i] + " " + tokens[i + 1]);
                }
                else if (tags[i].Equals("RB") || tags[i].Equals("RBR") || tags[i].Equals("RBS"))
                {
                    if ((i + 1) < tags.Length && tags[i + 1].Equals("JJ"))
                    {
                        if ((i + 2) >= tags.Length || !(tags[i + 2].Equals("NN") || tags[i + 2].Equals("NNS")))
                            phrases.Add(tokens[i] + " " + tokens[i + 1]);
                    }
                    else if ((i + 1) < tags.Length && (tags[i + 1].Equals("VB") || tags[i + 1].Equals("VBD") || tags[i + 1].Equals("VBN") || tags[i + 1].Equals("VBG")))
                        phrases.Add(tokens[i] + " " + tokens[i + 1]);

                }
            }
            foreach (string s in phrases)
            {
                //System.Console.WriteLine(s);
                debug.Print(s + "\n");
            }
            return phrases;
        }

        double SemanticOrientation(List<string> phrases, string review)
        {
            List<double> SO = new List<double>();
            int p,q=0,temp;
            foreach (string phrase in phrases)
            {
                double posHits = NumOfHits(Uri.EscapeUriString("\"" + phrase + "\"" + "AROUND(10)" + "\"excellent\"")) + 0.01;
                double negHits = NumOfHits(Uri.EscapeUriString("\"" + phrase + "\"" + "AROUND(10)" + "\"poor\"")) + 0.01;
                System.Console.WriteLine(posHits + " " + negHits);
                double so = (posHits / negHits) * (451000000.0 / 473000000.0);
                so = (Math.Log(so) / Math.Log(2));
                SO.Add(so);
                //gui
                temp = (review+q).IndexOf(phrase);
                if (temp == -1) continue;
                p = q; q = temp;
                string sub = review.Substring(p, q-p);
                ReviewBlock.Inlines.Add(sub);
                p = q; q += phrase.Length;
                
                sub = review.Substring(p, q-p);
                if (so > 0) ReviewBlock.Inlines.Add(new Run(sub) { Foreground = Brushes.LightGreen, ToolTip = Math.Round(so,3).ToString() });
                else if (so < 0) ReviewBlock.Inlines.Add(new Run(sub) { Foreground = Brushes.OrangeRed, ToolTip = Math.Round(so, 3).ToString() });
            }
            if (q != review.Length) ReviewBlock.Inlines.Add(review.Substring(q,review.Length-q));
            foreach (double d in SO)
            {
                System.Console.WriteLine(d);
                debug.Print(d.ToString() + "\n");
            }
            double avg;
            if (SO.Count > 0)
                avg = SO.Average();
            else avg = 0.0;
            System.Console.WriteLine("Avg: {0}", avg);
            debug.Print("Avg: " + avg);
            if (avg >= 0)
            {
                Score.Foreground = Brushes.Green;
                Score.Content = "Score: " + Math.Round(avg, 3).ToString() + "\nThe review is Positive";
            }
            else
            {
                Score.Foreground = Brushes.Red;
                Score.Content = "Score: " + Math.Round(avg, 3).ToString() + "\nThe review is Negative";
            }
            return avg;
        }

        long NumOfHits(string phrase)
        {
            HtmlAgilityPack.HtmlWeb web = new HtmlAgilityPack.HtmlWeb();
            HtmlAgilityPack.HtmlDocument htmlDoc = web.Load("https://www.google.com/search?q=" + phrase);
            if (htmlDoc.ParseErrors != null && htmlDoc.ParseErrors.Count() > 0)
            {
                // Handle any parse errors as requiredcw        
                System.Console.WriteLine("error");
                debug.Print("error\n");
                return -1;
            }
            else if (htmlDoc.DocumentNode != null)
            {
                HtmlAgilityPack.HtmlNode node = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='resultStats']");
                Regex re = new Regex(@"[1-9](?:\d{0,2})(?:,\d{3})*(?:\.\d*[1-9])?|0?\.\d*[1-9]|0");
                String result = re.Match(node.InnerHtml).Value;
                long hits = 0;
                if (result.Contains(","))
                    hits = long.Parse(result.Replace(",", ""));
                //System.Console.WriteLine(hits);
                return hits;
            }
            return -1;
        }

        string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        void ScrapePage(string url)
        {
            HtmlDocument doc = new HtmlDocument();
            doc = new HtmlWeb().Load(url);
            author = new List<String>();
            heading = new List<String>();
            comments = new List<String>();
            grade=new List<int>();
            if (doc.DocumentNode != null)
            {
                HtmlNodeCollection reviewer, review, title;
                reviewer = review = title = null;
                HtmlNodeCollection rating = null;
                if (url.Contains("amazon.com"))
                {
                    reviewer = doc.DocumentNode.SelectNodes("//span[@class='a-size-normal']");
                    review = doc.DocumentNode.SelectNodes("//div[contains(@id,'revData-dpReviewsMostHelpful')]/div[@class='a-section']");
                    title = doc.DocumentNode.SelectNodes("//a[@class='a-link-normal a-text-normal a-color-base'][2]/span[@class='a-size-base a-text-bold']");
                    rating = doc.DocumentNode.SelectNodes("//a[@class='a-link-normal a-text-normal a-color-base'][1]/i[contains(@class,'a-icon a-icon-star a-star')]");
                }
                else if (url.Contains("amazon.in"))
                {
                    reviewer = doc.DocumentNode.SelectNodes("//span[@class='txtsmall']");
                    review = doc.DocumentNode.SelectNodes("//div[@class='drkgry']");
                    title = doc.DocumentNode.SelectNodes("//a[@class='txtlarge gl3 gr4 reviewTitle valignMiddle']/strong");
                    rating = doc.DocumentNode.SelectNodes("//div[@class='mt4 ttl']/span[contains(@class,'swSprite s_star')]");
                }
                else if (url.Contains("flipkart.com"))
                {
                    reviewer = doc.DocumentNode.SelectNodes("//div[@class='line'][2]");
                    review = doc.DocumentNode.SelectNodes("//div[@class='lastUnit size4of5 section2']/p[@class='line bmargin10']");
                    title = doc.DocumentNode.SelectNodes("//div[@class='line fk-font-normal bmargin5 dark-gray']/strong");
                    rating = doc.DocumentNode.SelectNodes("//div[@class='fk-stars']/div[@class='rating']");
                }
                for (int i = 0; i < Math.Min(reviewer.Count, Math.Min(review.Count, title.Count)); i++)
                {
                    string str = title[i].InnerText; string str1 = review[i].InnerHtml;
                    str = str.Replace("\n", " ");
                    str1 = str1.Replace("\n", " "); str1 = str1.Replace(".", ". "); 
                    str1 = System.Text.RegularExpressions.Regex.Replace(str1, "<br>", ". "); 
                    //str1 = Truncate(str1, 250);
                    heading.Add(str);
                    comments.Add(str1);
                    //System.Console.WriteLine(review[i].InnerText);
                    str = reviewer[i].InnerText; 
                    str = str.Replace("\n", ""); str = str.Replace("  ", "");
                    //str1 = "";str1= rating[i].InnerText;str1 = str1.Replace("\n", ""); str1 = str1.Replace("  ", "");
                    author.Add(str + "\n");
                    //System.Console.WriteLine(reviewer[i].InnerText);
                    //debug.Print("rating: "+rating[i].InnerText);
                    grade.Add(rating[i].InnerText[0] - 48);
                    debug.Print(grade.Last().ToString());
                }

            }
        }

        void Program(int i = 0)
        {
            //string review = "I do not know ,man. It was ok I guess. Could have been better.";
            //string review = "What a great phone,man. Could have been better,but i am happy with its performance.";
            
            //System.Console.WriteLine("Review {0}:{1}", i + 1, author[i]);
            //debug.Print("Review " + (i + 1) + ":" + author[i] + "\n");
            //System.Console.WriteLine(comments[i]);
            //debug.Print(comments[i]);

            ReviewBlock.Text = string.Empty;
            ReviewBlock.Inlines.Add(new Run(heading[i]+"\n") { FontWeight = FontWeights.Bold, FontSize = 15 });
            ReviewBlock.Inlines.Add(new Run(author[i]+"\n") { Foreground=Brushes.Blue, TextDecorations= TextDecorations.Underline});
            string[] tokens = Tokenizer(heading[i]+comments[i]);
            double score=SemanticOrientation(ExtractPhrases(POSTagger(tokens), tokens), comments[i]);
            
            //user rating gui
            int l = 40;
            var fill = new Uri("pack://application:,,,/star_gold.jpg");
            var fill_bitmap = new BitmapImage(fill);
            var empty = new Uri("pack://application:,,,/star_empty.jpg");
            var empty_bitmap = new BitmapImage(empty);
            /*int j;
            for (j = 0; j <= grade[i]; ++j, l+=50)
            {
                Image star = new Image();
                star.Width = 25; star.Height = 25; star.Margin = new Thickness(l, -25, 0, 0); star.Source = fill_bitmap; star.Stretch = Stretch.UniformToFill;
                RatingGrid.Children.Add(star);
            }
            for (; j < 5; ++j, l += 50)
            {
                Image star = new Image();
                star.Width = 25; star.Height = 25; star.Margin = new Thickness(l, -25, 0, 0); star.Source = empty_bitmap; star.Stretch = Stretch.UniformToFill;
                RatingGrid.Children.Add(star);
            }
            //predicted rating gui
            fill = new Uri("pack://application:,,,/star_blue.jpg");
            fill_bitmap = new BitmapImage(fill);
            int p_rating=0;
            if (score >= 0) p_rating = 5;
            else p_rating = 1;
            l = 40;
            for (j = 0; j < p_rating; ++j, l += 50)
            {
                Image star = new Image();
                star.Width = 25; star.Height = 25; star.Margin = new Thickness(l, 30, 0, 0); star.Source = fill_bitmap; star.Stretch = Stretch.UniformToFill;
                RatingGrid.Children.Add(star);
            }
            for (; j < 5; ++j, l += 50)
            {
                Image star = new Image();
                star.Width = 25; star.Height = 25; star.Margin = new Thickness(l, 30, 0, 0); star.Source = empty_bitmap; star.Stretch = Stretch.UniformToFill;
                RatingGrid.Children.Add(star);
            }*/
        }
    }
}
