using GemCard;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace ECardReader
{
    public partial class Form1 : Form
    {
        private string reader;
        private CardBase m_iCard = null;
        private APDUParam m_apduParam = null;
        private List<string> readerList = new List<string>();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SelectICard();
            SetupReaderList();
        }

        private void SelectICard()
        {
            try
            {
                if (m_iCard != null)
                    m_iCard.Disconnect(DISCONNECT.Unpower);

                m_iCard = new CardNative();
                richTextBoxLog.Text += "CardNative implementation used";

                m_iCard.OnCardInserted += new CardInsertedEventHandler(m_iCard_OnCardInserted);
                m_iCard.OnCardRemoved += new CardRemovedEventHandler(m_iCard_OnCardRemoved);
            }
            catch (Exception ex)
            {
                richTextBoxLog.Text += ex.Message;
            }
        }

        private void SetupReaderList()
        {
            try
            {
                string[] sListReaders = m_iCard.ListReaders();

                if (sListReaders != null)
                {
                    for (int nI = 0; nI < sListReaders.Length; nI++)
                    {
                        readerList.Add(sListReaders[nI]);
                        log("Found" + sListReaders[nI]);
                    }
                    log("Selected " + sListReaders[0]);
                    //// Start waiting for a card
                    reader = sListReaders[0];
                    m_iCard.StartCardEvents(reader);
                    log("Waiting for a card");
                }
                else
                {
                    log("Found no smartcardreaders");
                }
            }
            catch (Exception ex)
            {
                richTextBoxLog.Text += ex.Message;
            }
        }

        private delegate void logDelegate(string text);

        private void log(string p)
        {
            richTextBoxLog.Text += p + "\n";
        }

        /// <summary>
        /// CardInsertedEventHandler
        /// </summary>
        private void m_iCard_OnCardInserted(string reader)
        {
            richTextBoxLog.Invoke(new logDelegate(log), new object[] { "Card inserted" });
            try
            {
                var hexInfo = Bytes2String(invokeSmartcard());
                richTextBoxLog.Invoke(new logDelegate(log), new object[] { hexInfo });
                richTextBoxLog.Invoke(new logDelegate(log), new object[] { parse(hexInfo) });
            }
            catch (SmartCardException exSC)
            {
                richTextBoxLog.Invoke(new logDelegate(log), new object[] { exSC.Message });
            }
            catch (Exception ex)
            {
                richTextBoxLog.Invoke(new logDelegate(log), new object[] { ex.Message });
            }
        }

        /// <summary>
        /// CardRemovedEventHandler
        /// </summary>
        private void m_iCard_OnCardRemoved(string reader)
        {
            richTextBoxLog.Invoke(new logDelegate(log), new object[] { "Card removed" });
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            {
                try
                {
                    m_iCard.Disconnect(DISCONNECT.Unpower);
                    m_iCard.StopCardEvents();
                }
                catch
                {
                }
            }
        }

        private APDUParam BuildParam(string P1, string P2, string Le)
        {
            byte bP1 = byte.Parse(P1, NumberStyles.AllowHexSpecifier);
            byte bP2 = byte.Parse(P2, NumberStyles.AllowHexSpecifier);
            byte bLe = byte.Parse(Le);

            APDUParam apduParam = new APDUParam();
            apduParam.P1 = bP1;
            apduParam.P2 = bP2;
            apduParam.Le = bLe;

            // Update Current param
            m_apduParam = apduParam.Clone();

            return apduParam;
        }

        private byte[] invokeSmartcard()
        {
            //i blanked the commands, and parameters - i am not sure if i am allowed to publish them
            APDUCommand apduEcard1 = new APDUCommand(0x00, 0x00, 04, 0x00, null, 0);
            APDUCommand apduEcard2 = new APDUCommand(0x00, 0x00, 0, 0, null, 0);
            APDUCommand apduEcard3 = new APDUCommand(0x00, 0x00, 00, 0x00, null, 255);
            APDUResponse apduResp;

            CardNative iCard = new CardNative();
            iCard.Connect(reader, SHARE.Shared, PROTOCOL.T0orT1); //connected
            // Ecard1
            byte[] data1 = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            APDUParam apduParam = new APDUParam();
            apduParam.Data = data1;
            apduEcard1.Update(apduParam);
            apduResp = iCard.Transmit(apduEcard1);
            // Ecard2
            apduParam.Data = new byte[] { 0x00, 0x00 };
            apduEcard2 = new APDUCommand(0x00, 0x00, 00, 0x0C, apduParam.Data, 0);
            apduEcard2.Update(apduParam);
            apduResp = iCard.Transmit(apduEcard2);
            // Ecard3
            apduParam.Data = new byte[] { 0 };
            apduEcard3 = new APDUCommand(0x00, 0x00, 00, 0x00, null, 255);
            apduResp = iCard.Transmit(apduEcard3);
            return apduResp.Data;
        }

        private string convertHexToAscii(string info)
        {
            //http://msdn.microsoft.com/en-us/library/bb311038.aspx
            string hexValues = info;
            string[] hexValuesSplit = hexValues.Trim().Split(' ');
            string returner = string.Empty;
            try
            {
                foreach (string hex in hexValuesSplit)
                {
                    if (hex != "  ")
                    {
                        // Convert the number expressed in base-16 to an integer.
                        int value = Convert.ToInt32(hex, 16);
                        // Get the character corresponding to the integral value.
                        string stringValue = Char.ConvertFromUtf32(value);
                        char charValue = (char)value;
                        //hex, value, stringValue, charValue);
                        returner += charValue;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            string returner1 = string.Empty;
            foreach (var item in returner.Split(new char[] { '\b', '\n', '\r', '\0', '\f' }))
            {
                returner1 += item + "\n";
            }
            return returner1;
        }

        private static string DecodeBytes2String(byte[] bytes)
        {
            return System.Text.Encoding.GetEncoding(1252).GetString(bytes);
        }

        private static string Bytes2String(byte[] data)
        {
            StringBuilder sDataOut;

            if (data != null)
            {
                sDataOut = new StringBuilder(data.Length * 2);
                for (int nI = 0; nI < data.Length; nI++)
                    sDataOut.AppendFormat("{0:X02} ", data[nI]);
            }
            else
                sDataOut = new StringBuilder();

            return sDataOut.ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            richTextBoxLog.Text = parse(richTextBox1.Text);
        }

        private string parse(string p)
        {
            var indexSvnr = p.IndexOf(Properties.Settings.Default.preSvnr, 0) + Properties.Settings.Default.preSvnr.Length + 1;
            //von ende des prefix 30 zeichen (svnr ist immer gleich lang)
            string svnr = p.Substring(indexSvnr, 30);

            var indexFirstName = p.IndexOf(Properties.Settings.Default.preFirstName, indexSvnr) + Properties.Settings.Default.preFirstName.Length + 8;
            //von ende des bekannten prefix +8 unbekannte zeichen bis 6 zeichen vor dem bekannten prefixteil von prelastname
            int firstNameLength = p.IndexOf(Properties.Settings.Default.preLastName, indexSvnr) - 6 - indexFirstName;
            string firstName = p.Substring(indexFirstName, firstNameLength);

            var indexPreLastName = p.IndexOf(Properties.Settings.Default.preLastName, indexFirstName) + Properties.Settings.Default.preLastName.Length + 10;
            int lastnameLength = p.IndexOf(Properties.Settings.Default.preBirthdate, indexPreLastName) - indexPreLastName;
            string lastName = p.Substring(indexPreLastName, lastnameLength);

            var indexBirthdate = p.IndexOf(Properties.Settings.Default.preBirthdate, indexPreLastName) + Properties.Settings.Default.preBirthdate.Length + 1;
            string Brithdate = p.Substring(indexBirthdate, 24);

            var indexSex = p.IndexOf(Properties.Settings.Default.preSex, indexBirthdate) + Properties.Settings.Default.preSex.Length + 1;
            //immer 2 zeichen lang
            string Sex = p.Substring(indexSex, 3);

            return convertHexToAscii(svnr) + "\n" + convertHexToAscii(firstName) + "\n" + convertHexToAscii(lastName) + "\n" + convertHexToAscii(Brithdate) + "\n" + convertHexToAscii(Sex) + "\n";
        }
    }
}