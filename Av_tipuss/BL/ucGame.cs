using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic.Devices;
using System.Xml.Linq;
using System.IO;
using System.Threading;

namespace BL
{
    //1. הצהרה על דלגט
    public delegate void dComputerFinishedEventHandler(List<List<ccCard>> cards);

    public delegate void dLoadChangesFinishedEventHandler(string filePath); 

    public partial class ucGame : UserControl//מחלקה שמטפלת בניהול המשחק מאתחלת את הקופה לוחות השחקנים סדר התורות וכו
    {
        public ucPlayerBoard[] playersBoards;
        public List<List<ccCard>> cloneMainBoard=new List<List<ccCard>>(), mainBoard=new List<List<ccCard>>();
        public List<ccCard> box = new List<ccCard>();
        public int turn;
        public int numOfPlayers;
        public Random r=new Random();
        public ucPlayerBoard currentPlayer;
        public ucPlayerBoard clonePlayerBoard;

        public string path;
        public bool netWorkGame = false; 
        public bool NewPlayerNetwork;
        public string currentPlayerName;
        public bool PlayerClosed = false;

        public byte[,] checkMat;
        public List<List<ccCard>> BestBoard = new List<List<ccCard>> { };
        public int maxScore = 0;
        public byte joker_cnt = 0;
        public byte must_joker_cnt = 0;
        //2. הגדרת אוונט מסוג הדלגט
        public event dComputerFinishedEventHandler eComputerFinished;
        public event dLoadChangesFinishedEventHandler eLoadChangesFinished; 
        Audio a = new Audio();
        public List<List<ccCard>> cmainBoard;
        public List<List<ccCard>> arrangedBoard=new List<List<ccCard>>();

        public List<Point> Seriaslocation = new List<Point> { }; // עבור רשת

        public List<List<ccCard>> OptionalsSerias = new List<List<ccCard>>();

        public ucGame(int _numOfPlayers, List<string> name, List<bool> kindPlayer, bool networkGame,string currUserName)//ekindCard kind,אמור לקבל גם:
        {
            InitializeComponent();
            numOfPlayers = _numOfPlayers;
            #region אתחול כרטיסים בקופסה
            
            ccCard tempCard;
            byte k,j;
            for(int i=0; i<4 ; i++)
            {
                for (j = 0; j < 2; j++)//פעמיים כל צבע
                    for (k = 1; k < 14; k++)
                    {
                        tempCard = new ccCard(k, (eColor)i);
                        tempCard.Status = eStatus.Box;
                        box.Add(tempCard);                        
                    }
                tempCard = new ccCard(14,(eColor)i);
                tempCard.Status = eStatus.Box;
                box.Add(tempCard);//joker   

            }
            #endregion
            currentPlayerName = currUserName;
            #region אתחול כרטיסים בלוחות השחקנים
            playersBoards = new ucPlayerBoard[numOfPlayers];
            int rand;
            for (int i = 0; i < numOfPlayers; i++)
            {
                
                List<ccCard> tempList=new List<ccCard>();   
                for (j = 0; j < 14; j++)
                {
                    bool flRet;
                    do
                    {
                        flRet = false;
                        rand=r.Next(107);
                        if(box[rand].Status!=eStatus.Box)
                            flRet=true;
                        if (!netWorkGame && box[rand].Number == 14)
                            flRet = true;
                    } while (flRet);
                    box[rand].Status = eStatus.Player;
                    tempList.Add(box[rand]);
                }
                playersBoards[i] = new ucPlayerBoard(tempList,name[i],kindPlayer[i]);
            }
            #endregion
            //הגרלת תור
            do
            {
                turn = r.Next(numOfPlayers);
            } while (playersBoards[turn].computer);


            #region network2 //@@
            netWorkGame = networkGame;

            if (netWorkGame)
            {

                int drive = Microsoft.VisualBasic.FileIO.FileSystem.Drives.Count;
                int i;
                for (i = 0; i < drive; i++)
                {
                    if (Microsoft.VisualBasic.FileIO.FileSystem.Drives[i].DriveType == DriveType.Removable)
                    {
                        path = Microsoft.VisualBasic.FileIO.FileSystem.Drives[i].Name;
                        break;
                    }
                }


                if (File.Exists(path + name[0] + name[1] + ".xml"))
                {

                    NewPlayerNetwork = false; // האם זה שחקן מצטרף או חדש- כרגע מצטרף
                    PlayerClosed = false;

                    try
                    {

                       // LoadUcGame(path + name[0] + name[1]);
                        mainBoard.Clear();
                        LoadUcGameFirst(path + name[0] + name[1]);

                        //עבור טעינת מיקומי הפאנלים formGame - זריקת ארוע ל 
                        if (eLoadChangesFinished != null)
                            eLoadChangesFinished(path + name[0] + name[1]);

                        File.Delete(path + name[0] + name[1] + ".xml");
                        
                        //ChangeTurn();
                    }
                    
                    catch (Exception ex) { MessageBox.Show(ex.ToString()); }

                }
                else
                {
                    NewPlayerNetwork = true;
                    saveStatusNetwork();
                }
            }
            fswNetork.Path = path;
            //*********************************************

            #endregion
            MessageBox.Show("." + playersBoards[turn].name + " random to play first ", "Rammi-start game", MessageBoxButtons.OK, MessageBoxIcon.Information);//@@
            if(netWorkGame)
            {
                if (playersBoards[0].name == currentPlayerName)
                    Show_Cards(0);
                else
                    Show_Cards(1);
            }
            else


            play();
            //check();
             if(currentPlayerName != playersBoards[turn].name)
                timer1.Start();
            
        }

        private void LoadUcGameFirst(string folderPath)//###
        {
            eColor tmpec = new eColor();
            eStatus tmpes = new eStatus();
            string curName;
            List<ccCard> curCards = new List<ccCard> { };
            //InitializNamesAttrib();
            // מתוך הנחה שמדובר במקסימום 2 שחקנים אז התור הקודם הוא או 1 או 0
            int beforeTurn = turn == 0 ? 1 : 0;

            //XDocument doc = XDocument.Load(path + "\\" + folderPath + "\\lastGame.xml");// לא בטוח טוב
            XDocument doc = XDocument.Load(folderPath + ".xml");
            var prop = doc.Element("properties");
            // שליפת התור הנוכחי
            turn = (int)prop.Attribute("turn");

            // שליפת אבני הקופה
            box.Clear();  //ריקון אבני הקופה כדי לאתחלה לפי המשחק השמור
            foreach (XElement crd in prop.Element("box").Elements())
            {
                int curNum = (int)crd.Attribute("number");
                string curColor = (string)crd.Attribute("color");
                string stt = (string)crd.Attribute("status");
                tmpec = ToEColor(curColor);
                tmpes = ToEStatus(stt);
                box.Add(new ccCard((byte)curNum, tmpec, tmpes));

            }
            // שליפת מאפייני השחקנים ואבניהם
            for (int i = 0; i < numOfPlayers; i++)
            {

                curName = (string)prop.Element("player" + (i + 1)).Attribute("name");
                
                var curPlyr = prop.Element("player" + (i + 1));
                //ריקון רשימת הקלפים של השחקן כדי לאתחלה לפי המשחק השמור
                playersBoards[i].listCards.Clear();
                curCards.Clear();
                for (int j = 0; j < (int)curPlyr.Attribute("cntCard"); j++)
                {
                    int curNum = (int)curPlyr.Element("card" + (j + 1)).Attribute("number");
                    string curColor = (string)curPlyr.Element("card" + (j + 1)).Attribute("color");
                    tmpec = ToEColor(curColor);
                    curCards.Add(new ccCard((byte)curNum, tmpec));
                }
                playersBoards[i] = new ucPlayerBoard(curCards.ToList(), curName, false);
            }
            // שליפת לוח המשחק
            if (mainBoard.Count > 0)
                mainBoard.Clear(); //ריקון לוח המשחק כדי לאתחלה לפי המשחק השמור
            else
                mainBoard = new List<List<ccCard>> { };
            foreach (XElement seria in prop.Element("mainBoard").Elements())
            {
                List<ccCard> tmp = new List<ccCard> { };
                int i = 0;

                foreach (XElement crd in seria.Elements())
                {
                    int curNum = (int)crd.Attribute("number");
                    string curColor = (string)crd.Attribute("color");
                    string stt = (string)crd.Attribute("status");
                    tmpec = ToEColor(curColor);
                    tmpes = ToEStatus(stt);
                    tmp.Add(new ccCard((byte)curNum, tmpec, tmpes));
                }
                mainBoard.Add(tmp);
                i++;
            }
            //Show_Cards();
        }
        public void LoadUcGame(string folderPath)
        {


            eColor tmpec = new eColor();
            eStatus tmpes = new eStatus();

            List<ccCard> curCards = new List<ccCard> { };
            //InitializNamesAttrib();
            // מתוך הנחה שמדובר במקסימום 2 שחקנים אז התור הקודם הוא או 1 או 0
            int beforeTurn = turn == 0 ? 1 : 0;

            //XDocument doc = XDocument.Load(path + "\\" + folderPath + "\\lastGame.xml");// לא בטוח טוב
            XDocument doc = XDocument.Load(folderPath + ".xml");
            var prop = doc.Element("properties");

            // שליפת התור הנוכחי
            turn = (int)prop.Attribute("turn");

            // שליפת אבני הקופה
            box.Clear();  //ריקון אבני הקופה כדי לאתחלה לפי המשחק השמור
            foreach (XElement crd in prop.Element("box").Elements())
            {
                int curNum = (int)crd.Attribute("number");
                string curColor = (string)crd.Attribute("color");
                string stt = (string)crd.Attribute("status");
                tmpec = ToEColor(curColor);
                tmpes = ToEStatus(stt);
                box.Add(new ccCard((byte)curNum, tmpec, tmpes));

            }

            // שליפת לוח המשחק
            if (mainBoard.Count > 0)
                mainBoard.Clear(); //ריקון לוח המשחק כדי לאתחלה לפי המשחק השמור
            else
                mainBoard = new List<List<ccCard>> { };
            foreach (XElement seria in prop.Element("mainBoard").Elements())
            {
                List<ccCard> tmp = new List<ccCard> { };
                int i = 0;

                foreach (XElement crd in seria.Elements())
                {
                    int curNum = (int)crd.Attribute("number");
                    string curColor = (string)crd.Attribute("color");
                    string stt = (string)crd.Attribute("status");
                    tmpec = ToEColor(curColor);
                    tmpes = ToEStatus(stt);
                    tmp.Add(new ccCard((byte)curNum, tmpec, tmpes));
                }
                mainBoard.Add(tmp);
                i++;
            }
            //Show_Cards();


        }
        public void startPlayNetwork(int _turn) 
        {
            turn = _turn;
            //if(currentPlayerName != playersBoards[turn].name)
            //    timer1.Start();
        }
        public ucGame()
        {
            InitializeComponent();
        }
        public void check()
        {
            cmainBoard = new List<List<ccCard>> { };
            //check
            List<ccCard> c = new List<ccCard> { };
            List<ccCard> c1 = new List<ccCard> { };
            //c.Add(new ccCard(3, eColor.Blue));
            c.Add(new ccCard(12, eColor.Red));
            c.Add(new ccCard(14, eColor.Green));
            c.Add(new ccCard(12, eColor.Green));
           // c.Add(new ccCard(7, eColor.Blue));
            cmainBoard.Add(c);

            //c1.Add(new ccCard(3, eColor.Blue));
            //c1.Add(new ccCard(3, eColor.Green));
            //c1.Add(new ccCard(3, eColor.Red));
            //cmainBoard.Add(c1);
            //cmainBoard[0].Add(new ccCard(3, eColor.Blue));
            //cmainBoard[0].Add(new ccCard(3, eColor.Red));
            //cmainBoard[0].Add(new ccCard(3, eColor.Yellow));

            //cmainBoard[1].Add(new ccCard(3, eColor.Yellow));
            //cmainBoard[1].Add(new ccCard(4, eColor.Yellow));
            //cmainBoard[1].Add(new ccCard(5, eColor.Yellow));
            //cmainBoard[1].Add(new ccCard(6, eColor.Yellow));
            //cmainBoard[1].Add(new ccCard(7, eColor.Yellow));

            //cmainBoard[1].Add(new ccCard(1, eColor.Red));
            //cmainBoard[1].Add(new ccCard(14, eColor.Red));
            //cmainBoard[1].Add(new ccCard(3, eColor.Red));

            if (legal(cmainBoard))
                MessageBox.Show("Good Sidra!!!");
            else
                MessageBox.Show("Bad Sidra!!!");
        }

        public List<List<ccCard>> endTurn(List<List<ccCard>> checkMainBoard, List<ccCard> _currentPlayer)
        {
            if (legal(checkMainBoard))
            {
                //MessageBox.Show("יצרת סדרות חוקיות");
                //עדכון מצב הכרטיסים הנכון
                for (int i = 0; i < checkMainBoard.Count; i++)
                   foreach (ccCard card in checkMainBoard[i])
                        if (card.Status==eStatus.Player)
                           card.Status = eStatus.mainBoard;
                
                //עדכוני הליסטים
                mainBoard = checkMainBoard;
                playersBoards[turn].listCards = _currentPlayer;
            }

            else 
            {
                
                a.Play(RammyCube.Properties.Resources.Speech_Off, Microsoft.VisualBasic.AudioPlayMode.Background);   
                //יש לעדכן את הפאנלים בליסטים השמורים
                MessageBox.Show("your serias are not available!");
                mainBoard = cloneMainBoard.ToList();
            }
            if (netWorkGame)//@@
            {
                //@@ שמירת מצב נוכחי עבור משחק ברשת
                saveStatusNetwork();
                ChangeTurn();
                //###Show_Cards();
                MessageBox.Show(playersBoards[turn].name + " it is your turn to play "); //TODO: !לוודא שאכן מודיע לשני השחקנים על התור הנכון
                play();
                if (currentPlayerName != playersBoards[turn].name)
                {
                    timer1.Start();
                    //ChangeTurn();
                }
                
            }
            
            else//@@
            {
            }

            return mainBoard;
            
        }
        public void saveStatusNetwork()//###
        {
            XElement prop = new XElement(
                 new XElement("properties", new XAttribute("numOfPlayers", numOfPlayers), new XAttribute("turn", turn)));


            for (int i = 0; i < numOfPlayers; i++)
            {
                int j;
                XElement plyrs = new XElement("player" + (i + 1).ToString(),
                    new XAttribute("name", playersBoards[i].name));

                for (j = 0; j < playersBoards[i].listCards.Count; j++)
                {
                    plyrs.Add(new XElement("card" + (j + 1).ToString(),
                        new XAttribute("number", playersBoards[i].listCards[j].Number), new XAttribute("color", playersBoards[i].listCards[j].getColor)));

                }
                plyrs.Add(new XAttribute("cntCard", j));
                prop.Add(plyrs);
            }




            XElement xbox = new XElement("box");
            for (int i = 0; i < box.Count; i++)
            {
                xbox.Add(new XElement("card" + (i + 1).ToString(),
                     new XAttribute("number", box[i].Number), new XAttribute("color", box[i].getColor), new XAttribute("status", box[i].Status)));

            }
            prop.Add(xbox);

            XElement series = new XElement("mainBoard");
            for (int i = 0; i < mainBoard.Count; i++)
            {
                XElement seria = new XElement("seria" + (i + 1).ToString(),
                    new XAttribute("x", Seriaslocation[i].X), new XAttribute("y", Seriaslocation[i].Y));
                for (int j = 0; j < mainBoard[i].Count; j++)
                {
                    seria.Add(new XElement("card" + (j + 1).ToString(),
                     new XAttribute("number", mainBoard[i][j].Number), new XAttribute("color", mainBoard[i][j].getColor), new XAttribute("status", mainBoard[i][j].Status)));
                }
                series.Add(seria);
            }

            prop.Add(series);

            //prop.Save(playersBoards[turn].name + "\\lastGame.xml");
            prop.Save(path + playersBoards[0].name + playersBoards[1].name + ".xml");
        }
        public void initalizeLocations(List<Point> _Seriaslocation)
        {
            Seriaslocation = _Seriaslocation;
        }
        private void ChangeTurn()
        {
            //לקיחת קלף מהקופה
            int rand;
            //בדיקה אם קימים קלפים בקופה
            if (BoxNotEmpty())
            {
                bool flRet;
               
                do
                {
                    flRet = false;
                    rand = r.Next(box.Count());
                    if (box[rand].Status != eStatus.Box)
                        flRet = true;
                    if (!netWorkGame && box[rand].Number == 14)
                        flRet = true;
                } while (flRet);
                box[rand].Status = eStatus.Player;
                playersBoards[turn].listCards.Add(box[rand]);
            }
            else
                MessageBox.Show("the cards in the box were finished");
            //בדיקה אם המשחק הסתיים
            if (CheckGameFinished(playersBoards[turn].listCards))
            {
                a.Play(RammyCube.Properties.Resources.Boo, Microsoft.VisualBasic.AudioPlayMode.Background);   
                MessageBox.Show("Game over", playersBoards[turn].name+" You win!  Great!!");        
           
               
            }
            else
            {
                Show_Cards(turn);//###
                //מעבר תור
                turn++;
                if (turn == numOfPlayers)
                    turn = 0;
               
            }
        }

        public bool BoxNotEmpty()
        {
            if (box.Count == 0)
                return false;
            return true;
        }

        public bool CheckGameFinished(List<ccCard> listcards)
        {
            if (listcards.Count == 0)
                return true;
            return false;
        }
        public eColor ToEColor(string colorToParse)
        {
            eColor retColor = new eColor();
            switch (colorToParse)
            {
                case "Blue":
                    retColor = eColor.Blue;
                    break;
                case "Red":
                    retColor = eColor.Red;
                    break;
                case "Green":
                    retColor = eColor.Green;
                    break;
                case "Orange":
                    retColor = eColor.Orange;
                    break;
            }
            return retColor;
        }
         public eStatus ToEStatus(string statusToParse)
        {
            eStatus retStatus = new eStatus();
            switch (statusToParse)
            {
                case "Box":
                    retStatus = eStatus.Box;
                    break;
                case "mainBoard":
                    retStatus = eStatus.mainBoard;
                    break;
                case "Player":
                    retStatus = eStatus.Player;
                    break;
                          }
            return retStatus;
        }
        public void Show_Cards(int index)
        {
            int top = 0, left = ccCard.l_space_between_cards*3;
           // if (playersBoards[turn].name == "אני")
            {

                foreach (ccCard card in playersBoards[index].listCards)
                {
                    //הצגת הכרטיסים על המסך
                    if (card.Width != ccCard.width_card)
                    {
                        card.Width = ccCard.width_card;
                        card.Height = ccCard.height_card;
                        card.Font = new System.Drawing.Font("OCR A Extended", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(177)));
                    }
                    card.Top = top;
                    card.Left = left;

                    if (card.Left + ccCard.width_card > playersBoards[index].Width)
                    {
                        top += card.Height;
                        left = ccCard.l_space_between_cards*3;
                    }
                    card.Top = top;
                    card.Left = left;

                    left += card.Width + ccCard.l_space_between_cards;
                    card.Show();
                    card.BringToFront();
                    playersBoards[index].Controls.Add(card);
                }

            }
        }

        public void play()
        {
            
            //עבור שחקן רגיל           
            //שכפול לוח המשחק של השחקן
            cloneMainBoard.Clear();
            clonePlayerBoard = (ucPlayerBoard) playersBoards[turn].Clone();
            List<ccCard> temp = new List<ccCard> { };
                        for (int i = 0; i < mainBoard.Count; i++)
            {
             
                foreach (ccCard c in mainBoard[i])
                {
                    temp.Add(c);
                }
                cloneMainBoard.Add(temp.ToList());
                temp.Clear();
            }

           
           
        }

       

        private void SplitingSerias(List<ccCard> listToSplit, int count)
        {
            List<ccCard> temp = new List<ccCard>();
            for (int i = 0; i <= count-3; i++)
            {
                for (int j = i+3; j <= count; j++)
                {
                    temp.Clear();
                    for (int k = i; k < j; k++)
                    {
                        temp.Add(listToSplit[k]);
                    }
                    OptionalsSerias.Add(temp.ToList());
                }
            }
        }
        
     

       
        private List<List<ccCard>> copySeriasList(List<List<ccCard>> opSeriasMakor)
        {
            List<List<ccCard>> newList=new List<List<ccCard>>();
            List<ccCard> tempList;
            foreach (List<ccCard> itemRow in opSeriasMakor)
            {
                tempList = new List<ccCard>();
                foreach (ccCard itemCol in itemRow)
                    tempList.Add((ccCard)itemCol.Clone());
                newList.Add(tempList);
            }
            return newList;
        }

       

     

       

        /// <summary> 
        /// פונקציה הבודקת חוקיות הסדרות ברשימה הנשלחת אליה
        /// </summary>
        /// <param name="checkMainBoard">רשימת רשימות של אבני משחק עליהן מתבצעת הבדיקה</param>
        public bool legal(List<List<ccCard>> checkMainBoard)
        {
            //בדיקת חוקיות הלוח הראשי
            for (int i = 0; i < checkMainBoard.Count; i++)
            {
                //ראשית- אורך הסדרה חייב להיות גדול מ/שווה ל-3
                if (checkMainBoard[i].Count >= 3)
                {
                    //שליחה לפונקציות עזר הבודקות 2 אפשרויות לסדרה חוקית
                    //רק אם חוזרת תשובה חיובית מאחת מהן- הסדרה חוקית
                    if (!Asc(checkMainBoard[i]) && !sameColor(checkMainBoard[i]))
                        return false;
                }
                else
                    return false;
               
            }           
            return true;
        }
        //private void fswNetork_Changed(object sender, FileSystemEventArgs e)
        //{
        //    //@@
        //    // להמתין מעט לאחר השינוי לפני שטוענים
        //    Thread.Sleep(5000);
        //    try
        //    {
        //        if (currentPlayerName != playersBoards[turn].name)
        //        {
        //            XDocument doc = XDocument.Load(path + playersBoards[0].name + playersBoards[1].name + ".xml");
        //            var prop = doc.Element("status");
        //            if (prop != null && (string)prop.Attribute("status") == "finished")// אם השחקן השני יצא מהמשחק
        //            {
        //                PlayerClosed = true;
        //                MessageBox.Show(".השחקן השני מעוניין לצאת מהמשחק", "רמי", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //                File.Delete(path + playersBoards[0].name + playersBoards[1].name + ".xml");
        //                System.Media.SystemSounds.Beep.Play();

        //            }
        //            else
        //            {
        //                LoadUcGame(path + playersBoards[0].name + playersBoards[1].name);

        //                //עבור טעינת מיקומי הפאנלים formGame - זריקת ארוע ל 
        //                //if (eLoadChangesFinished != null)
        //                //eLoadChangesFinished(path + playersBoards[0].name + playersBoards[1].name);
        //            }
        //        }
        //        else
        //            Show_Cards();

        //        ChangeTurn();
        //        MessageBox.Show(playersBoards[turn].name + "תורך לשחק"); //TODO: !לוודא שאכן מודיע לשני השחקנים על התור הנכון
        //        play();
        //        //break;

        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.ToString());
        //    }

        //}
        public void setCurrentName(string p)
        {
            currentPlayerName = p;
        }
        /// <summary> 
        /// פונקצייה בוליאנית הבודקת האם הסדרה שקבלה מסודרת בסדר עולה
        /// </summary>
        /// <param name="checkList">רשימת אבני משחק עליהן מתבצעת הבדיקה</param>
        public bool Asc(List<ccCard> checkList)
        {
            int clr=(int)checkList[0].getColor;
            // משתנה המאותחל בספרה הראשונה של הסדרה ומקודם בכל איטרציה
            byte num = checkList[0].Number; 
            //לולאה העוברת על הרשימה
            for (int i = 1; i < checkList.Count; i++)
            {
                //בדיקה האם צבעי כל האבנים זהים
                if ((int)checkList[i].getColor == clr)
                {
                    num++;
                    // בדיקה האם הסידרה בסדר עולה
                    if (checkList[i].Number != num && checkList[i].Number != 14)
                        return false;
                }
                // טיפול בג'וקר
                else if (checkList[i].Number != 14)
                    return false;
                else
                    //קידום משתנה העזר
                    num++;
                  
            }
            return true;
        }
        /// <summary> 
        /// פונקצייה בוליאנית הבודקת האם הסדרה שקבלה הינה סדרת צבעים
        /// </summary>
        /// <param name="checkList">רשימת אבני משחק עליהן מתבצעת הבדיקה</param>
        public bool sameColor(List<ccCard> checkList)
        {
            //  אתחול משתנה עזר במספר הראשון שאמור להיות זהה בכל הסדרה
            byte num = checkList[0].Number;
            int clr;
            // אתחול מערך בוליאני המכיל אינדיקציות האם כבר קיים צבע זה בסדרה או לא
            bool [] colors=new bool[4]{false,false,false,false};
           
            //סידרה מסוג זה לא יכולה להכיל יותר מ-4 איברים
            if (checkList.Count <= 4)
            {               
               for (int i = 1; i < checkList.Count; i++)
               {
                   // טיפול בג'וקר
                   if (num == 14)
                    {
                        num = checkList[i++].Number;
                        continue;
                    }
                   // בדיקה על המספר- אם הוא זהה
                   if (checkList[i].Number == num || checkList[i].Number == 14)
                   {
                       clr = (int)checkList[i].getColor;
                       // בדיקה האם הצבע לא קיים עדיין בסדרה
                       if (!colors[clr] && checkList[i].Number!=14)
                           colors[clr] = true;
                       else if(checkList[i].Number != 14)
                           return false;
                   }
                   else
                       return false;
               }
               return true;
            }
            return false;
        }

        private void ucGame_Load(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            
            try
            {
                if (currentPlayerName != playersBoards[turn].name)
                {

                    try
                    {
                        XDocument doc = XDocument.Load(path + playersBoards[0].name + playersBoards[1].name + ".xml");
                        var prop = doc.Element("status");

                        if (prop != null && (string)prop.Attribute("status") == "finished")// אם השחקן השני יצא מהמשחק
                        {
                            timer1.Stop();
                            PlayerClosed = true;
                            MessageBox.Show("the second player want to go out the game", "Rammi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            File.Delete(path + playersBoards[0].name + playersBoards[1].name + ".xml");
                            System.Media.SystemSounds.Beep.Play();
                        }
                        //XDocument doc = XDocument.Load(path + playersBoards[0].name + playersBoards[1].name + ".xml");
                        //// אם השחקן השני יצא מהמשחק
                        //if (File.Exists(path + "exit.xml"))
                        //{
                        //    timer1.Stop();
                        //    PlayerClosed = true;
                        //    MessageBox.Show(".השחקן השני מעוניין לצאת מהמשחק", "רמי", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        //    File.Delete(path + "exit.xml");
                        //    System.Media.SystemSounds.Beep.Play();
                        //}
                        else
                        {
                            timer1.Stop();
                            mainBoard.Clear();
                            LoadUcGame(path + playersBoards[0].name + playersBoards[1].name);

                            //עבור טעינת מיקומי הפאנלים formGame - זריקת ארוע ל 
                            if (eLoadChangesFinished != null)
                                eLoadChangesFinished(path + playersBoards[0].name + playersBoards[1].name);

                            File.Delete(path + playersBoards[0].name + playersBoards[1].name + ".xml");
                            ChangeTurn();
                            Show_Cards(turn);
                            MessageBox.Show(playersBoards[turn].name + " it is your turn to play "); //TODO: !לוודא שאכן מודיע לשני השחקנים על התור הנכון
                            play();
                 
                        }
                    }
                    catch (Exception ex) { }
                }
                else
                {
                    timer1.Stop();
                    ChangeTurn();
                    Show_Cards(turn);
                    MessageBox.Show(playersBoards[turn].name + "it is your turn to playS "); //TODO: !לוודא שאכן מודיע לשני השחקנים על התור הנכון
                    play();
                }
                //break;
            }
        
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        
    }
}
