using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace SerialSMSSender {
    public partial class MainForm : Form {

        #region INITIALIZATIONS
        public MainForm() {
            InitializeComponent();
        }
        private void MainForm_Load(object sender, EventArgs e) {

            InitializePorts();
            InitializeGUI();

            Thread receiveMessageGateWayOneThread = new Thread(ReceiveMessageGateWayOne);
            Thread receiveMessageGateWayTwoThread = new Thread(ReceiveMessageGateWayTwo);

            Thread processCommandsThread = new Thread(ProcessCommands);
            Thread processOutboundsThread = new Thread(ProcessOutbounds);

            receiveMessageGateWayOneThread.Start();
            receiveMessageGateWayTwoThread.Start();
            processCommandsThread.Start();
            processOutboundsThread.Start();

            GetRoleRank("PROVINCIAL");

            //ClearMessagesOnGateWay(GateWayOnePort);
            //ClearMessagesOnGateWay(GateWayTwoPort);
            //ClearMessagesOnGateWay(SenderOnePort);
            //ClearMessagesOnGateWay(SenderTwoPort);
        }
        #endregion

        #region GLOBAL VARIABLES
        public string MySQLConnectionString = "datasource=127.0.0.1;port=3306;username=root;password=;database=allnetload";

        public string GateWayOnePort;
        public string GateWayTwoPort;
        public string SenderOnePort;
        public string SenderTwoPort;
        public string GlobeRetailerOnePort;
        public string GlobeRetailerTwoPort;
        public string GlobeRetailerThreePort;
        public string SmartRetailerPort;

        public bool GlobePortOneActive = true;
        public bool GlobePortTwoActive = true;
        public bool GlobePortThreeActive = true;
        public bool SmartPortActive = true;

        public bool SenderPortOneActive = true;
        public bool SenderPortTwoActive = true;
        #endregion

        #region THREAD - Receive Messages on Gateway One
        private void ReceiveMessageGateWayOne() {
            string[] splitDisplacerNewLine = new string[] { Environment.NewLine };
            string[] splitDisplacerCMT = new string[] { "+CMT" };

            string messageReceived;

            try {
                using (SerialPort gateWayOne = new SerialPort(GateWayOnePort, 115200)) {
                    gateWayOne.Open();
                    gateWayOne.NewLine = Environment.NewLine;
                    gateWayOne.Write("AT+CNMI=1,2,0,0,0\r");
                    Thread.Sleep(100);
                    gateWayOne.Write("AT\r\n");
                    Thread.Sleep(100);
                    gateWayOne.Write("AT+CMGF=1\r\n");
                    Thread.Sleep(100);

                    while (true) {
                        gateWayOne.Write("AT+CMGL=\"ALL\"\r\n");
                        Thread.Sleep(100);

                        string existing = gateWayOne.ReadExisting();

                        if (existing.Contains("+CMT")) {

                            this.Invoke((MethodInvoker)delegate {
                                homeTextBoxReceiving.Text = homeTextBoxReceiving.Text + existing;
                                homeTextBoxReceiving.SelectionStart = homeTextBoxReceiving.Text.Length;
                                homeTextBoxReceiving.ScrollToCaret();
                            });

                            messageReceived = existing.Split(splitDisplacerCMT, StringSplitOptions.None)[1];
                            messageReceived = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0] + Environment.NewLine + messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1];

                            string commandSent = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1].Trim();
                            string senderNumber = normalizeNumber(messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0].Split('"')[1].Trim());
                            string dateTime = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0].Split('"')[3];
                            dateTime = dateTime.Remove(dateTime.Length - 3, 3).Trim();

                            string referenceNumber = GenerateReferenceNumber();

                            string commandType = commandSent.Split(' ')[0].Trim();

                            if (IsCharactersOnly(commandType)) {
                                if (commandType == "REP") {
                                    if (IsCharactersOnly(commandSent.Split(' ')[1].Trim())) {
                                        commandType = commandType + " " + commandSent.Split(' ')[1].Trim();
                                    }
                                    else {
                                        commandType = commandType + " " + "LOAD";
                                    }
                                }
                            }
                            else {
                                commandType = "LOAD";
                            }

                            commandType = commandType.ToUpper();


                            if (UserExists(senderNumber)) {
                                RunDatabaseQuery("INSERT INTO commands (SenderNumber, Command, DateTime, Status, ReferenceNumber, CommandType) VALUES('" + senderNumber + "', '" + commandSent + "', '" + dateTime + "', 'PENDING', '" + referenceNumber + "', '" + commandType + "')");
                            }
                            else {
                                if (commandType == "LOGIN") {
                                    RunDatabaseQuery("INSERT INTO commands (SenderNumber, Command, DateTime, Status, ReferenceNumber, CommandType) VALUES('" + senderNumber + "', '" + commandSent + "', '" + dateTime + "', 'PENDING', '" + referenceNumber + "', '" + commandType + "')");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }
        }
        #endregion

        #region THREAD - Receive Messages on Gateway Two
        private void ReceiveMessageGateWayTwo() {
            string[] splitDisplacerNewLine = new string[] { Environment.NewLine };
            string[] splitDisplacerCMT = new string[] { "+CMT" };

            string messageReceived;

            try {
                using (SerialPort gateWayTwo = new SerialPort(GateWayTwoPort, 115200)) {
                    gateWayTwo.Open();
                    gateWayTwo.NewLine = Environment.NewLine;
                    gateWayTwo.Write("AT+CNMI=1,2,0,0,0\r");
                    Thread.Sleep(100);
                    gateWayTwo.Write("AT\r\n");
                    Thread.Sleep(100);
                    gateWayTwo.Write("AT+CMGF=1\r\n");
                    Thread.Sleep(100);

                    while (true) {
                        gateWayTwo.Write("AT+CMGL=\"ALL\"\r\n");
                        Thread.Sleep(100);

                        string existing = gateWayTwo.ReadExisting();

                        if (existing.Contains("+CMT")) {

                            this.Invoke((MethodInvoker)delegate {
                                homeTextBoxReceiving.Text = homeTextBoxReceiving.Text + existing;
                                homeTextBoxReceiving.SelectionStart = homeTextBoxReceiving.Text.Length;
                                homeTextBoxReceiving.ScrollToCaret();
                            });

                            messageReceived = existing.Split(splitDisplacerCMT, StringSplitOptions.None)[1];
                            messageReceived = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0] + Environment.NewLine + messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1];

                            string commandSent = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1].Trim();
                            string senderNumber = normalizeNumber(messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0].Split('"')[1].Trim());
                            string dateTime = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0].Split('"')[3];
                            dateTime = dateTime.Remove(dateTime.Length - 3, 3).Trim();

                            string referenceNumber = GenerateReferenceNumber();

                            string commandType = commandSent.Split(' ')[0].Trim();

                            if (IsCharactersOnly(commandType)) {
                                if (commandType == "REP") {
                                    if (IsCharactersOnly(commandSent.Split(' ')[1].Trim())) {
                                        commandType = commandType + " " + commandSent.Split(' ')[1].Trim();
                                    }
                                    else {
                                        commandType = commandType + " " + "LOAD";
                                    }
                                }
                            }
                            else {
                                commandType = "LOAD";
                            }

                            commandType = commandType.ToUpper();


                            if (UserExists(senderNumber)) {
                                RunDatabaseQuery("INSERT INTO commands (SenderNumber, Command, DateTime, Status, ReferenceNumber, CommandType) VALUES('" + senderNumber + "', '" + commandSent + "', '" + dateTime + "', 'PENDING', '" + referenceNumber + "', '" + commandType + "')");
                            }
                            else {
                                if (commandType == "LOGIN") {
                                    RunDatabaseQuery("INSERT INTO commands (SenderNumber, Command, DateTime, Status, ReferenceNumber, CommandType) VALUES('" + senderNumber + "', '" + commandSent + "', '" + dateTime + "', 'PENDING', '" + referenceNumber + "', '" + commandType + "')");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }
        }
        #endregion

        #region THREAD - Process Commands
        private void ProcessCommands() {
            while (true) {
                string query = "SELECT * FROM commands WHERE Status = 'PENDING'";

                DataTable dataTable = RunQueryDataTable(query);

                foreach (DataRow row in dataTable.Rows) {
                    string senderNumber = row["SenderNumber"].ToString();
                    string command = row["Command"].ToString();
                    string commandType = row["CommandType"].ToString();
                    string dateTime = row["DateTime"].ToString();
                    string status = row["Status"].ToString();
                    string referenceNumber = row["ReferenceNumber"].ToString();

                    if (commandType == "BAL") {
                        ProcessBalance(senderNumber, referenceNumber);
                    }
                    else if (commandType == "RET") {
                        ProcessRetailer(senderNumber, command, referenceNumber);
                    }
                    else if ((commandType == "DISTRIBUTOR") || (commandType == "DEALER") || (commandType == "MOBILE") || (commandType == "CITY") || (commandType == "PROVINCIAL")) {
                        ProcessRegistrationByRole(senderNumber, command, referenceNumber, commandType);
                    }
                    else if (commandType == "ADD") {
                        ProcessAdditionalAccount(senderNumber, command, referenceNumber);
                    }
                    else if (commandType.Contains("UP")) {
                        ProcessStatusUpgrade(senderNumber, command, referenceNumber);
                    }
                    else {
                        QueueOutbound("Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
                    }

                    RunDatabaseQuery("UPDATE commands SET Status = 'DONE' WHERE referenceNumber = '" + referenceNumber + "'");

                    Thread.Sleep(2000);
                }
            }
        }

        private void ProcessBalance(string senderNumber, string referenceNumber) {

            string message;
            string balance, pins;
            string query = "SELECT * FROM users WHERE PhoneNumber = '" + senderNumber + "'";

            DataTable dataTableBalance = RunQueryDataTable(query);

            try {
                balance = dataTableBalance.Rows[0]["Balance"].ToString();
                pins = dataTableBalance.Rows[0]["Pins"].ToString();

                message = "Current Balance:" + Environment.NewLine + "iLOAD : P" + FormatNumberWithComma(balance, true) + Environment.NewLine + "PINS: " + FormatNumberWithComma(pins, false) + Environment.NewLine + Environment.NewLine + DateTime.Now.ToString("MM-dd-yyyy h:mm");

                QueueOutbound(message, senderNumber, referenceNumber);
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }
        }

        private void ProcessRetailer(string senderNumber, string command, string referenceNumber) {

            try {
                command = command.Replace("RET ", "");

                string numberToRegister = command.Split('/')[0].Trim();
                string username = command.Split('/')[3];
                string password = command.Split('/')[1];
                string birthDate = command.Split('/')[4];
                string completeName = command.Split('/')[2];
                string address = command.Split('/')[5];

                string message;

                if (!UserExists(numberToRegister)) {

                    if (UsernameValid(username)) {

                        if (PasswordValid(password)) {

                            if (DateValid(birthDate)) {

                                RegisterUser(username, password, numberToRegister, completeName, birthDate, address, "RETAILER", senderNumber);

                                message = numberToRegister + " has been registered as a Techno User.";
                                QueueOutbound(message, senderNumber, referenceNumber);

                                Thread.Sleep(2000);

                                message = "Congratulations! You are now registered as Techno User. With USERNAME: " + username + " and PW: " + password + ". NEVER share this information to anyone. You may refer to the product guide for transactions.";
                                QueueOutbound(message, numberToRegister, referenceNumber);
                            }
                            else {
                                QueueOutbound("Registration Error: Birthday Bust Be mm-dd-yyyy.", senderNumber, referenceNumber);
                            }
                        }
                        else {
                            QueueOutbound("Registration Error: Password Must Be a 5 Digit Number.", senderNumber, referenceNumber);
                        }
                    }
                    else {
                        QueueOutbound("Registration Error: Username Must Be 8 Characters only. Letters and Numbers Only.", senderNumber, referenceNumber);
                    }
                }
                else {
                    QueueOutbound("Registration Error: Number already registered.", senderNumber, referenceNumber);
                }
            }
            catch {
                QueueOutbound("Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
            }
        }

        private void ProcessRegistrationByRole(string senderNumber, string command, string referenceNumber, string role) {
            
            try {
                command = command.Replace(role + " ", "");

                string numberToRegister = command.Split('/')[0].Trim();
                string activationCode = command.Split('/')[1].Trim();
                string username = command.Split('/')[4];
                string password = command.Split('/')[2];
                string birthDate = command.Split('/')[5];
                string completeName = command.Split('/')[3];
                string address = command.Split('/')[6];

                string message;

                if (!UserExists(numberToRegister)) {

                    if (UsernameValid(username)) {

                        if (PasswordValid(password)) {

                            if (DateValid(birthDate)) {

                                if (ActivationCodeUsable(activationCode, role)) {

                                    if(GetActivationCodeUser(activationCode) == "") {

                                        if(int.Parse(GetUserPins(senderNumber)) != 0) {

                                            RegisterUser(username, password, numberToRegister, completeName, birthDate, address, role, senderNumber);
                                            UseActivationCode(activationCode, username);
                                            DeductPin(senderNumber, "1");

                                            message = numberToRegister + " has been registered as a Techno User.";
                                            QueueOutbound(message, senderNumber, referenceNumber);

                                            Thread.Sleep(2000);

                                            message = "Congratulations! You are now registered as Techno User. With USERNAME: " + username + " and PW: " + password + ". NEVER share this information to anyone. You may refer to the product guide for transactions. You now have " + GetNumberOfRegistrationPins(role) + " pins";
                                            QueueOutbound(message, numberToRegister, referenceNumber);

                                            Thread.Sleep(2000);

                                            message = "Final Step: Get your Product code from your sponsor to register your account on website.";
                                            QueueOutbound(message, numberToRegister, referenceNumber);
                                        }
                                        else {
                                            QueueOutbound("Registration Error: You do not have enough pins to complete the registration.", senderNumber, referenceNumber);
                                        } 
                                    }
                                    else {
                                        QueueOutbound("Registration Error: Activation Already Used by " + GetActivationCodeUser(activationCode) + ".", senderNumber, referenceNumber);
                                    }
                                }
                                else {
                                    QueueOutbound("Registration Error: Invalid Activation Code.", senderNumber, referenceNumber);
                                }
                            }
                            else {
                                QueueOutbound("Registration Error: Birthday Bust Be mm-dd-yyyy.", senderNumber, referenceNumber);
                            }
                        }
                        else {
                            QueueOutbound("Registration Error: Password Must Be a 5 Digit Number.", senderNumber, referenceNumber);
                        }
                    }
                    else {
                        QueueOutbound("Registration Error: Username Must Be 8 Characters only. Letters and Numbers Only.", senderNumber, referenceNumber);
                    }
                }
                else {
                    QueueOutbound("Registration Error: Number already registered.", senderNumber, referenceNumber);
                }
            }
            catch{
                QueueOutbound("Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
            }
        }

        private void ProcessAdditionalAccount(string senderNumber, string command, string referenceNumber) {
            try {
                string activationCode = command.Split(' ')[1].Trim().Split('/')[0];
                string numberToAdd = command.Split(' ')[1].Trim().Split('/')[1];
                string userRole = GetUserRole(senderNumber);
                string activationCodeRole = GetActivationCodeRole(activationCode);
                string query;

                if (ActivationCodeUsable(activationCode, activationCodeRole)) {

                    if(GetActivationCodeUser(activationCode) == "") {

                        query = "UPDATE users SET Pins = '" + (int.Parse(GetUserPins(senderNumber)) + int.Parse(GetNumberOfRegistrationPins(activationCodeRole))).ToString() + "' WHERE PhoneNumber = '" + senderNumber + "'";
                        
                        RunDatabaseQuery(query);

                        UseActivationCode(activationCode, GetUserUsername(senderNumber));

                        QueueOutbound("Congratulations! You have added 1 " + activationCodeRole + " to your account. You now have additional " + FormatNumberWithComma(GetNumberOfRegistrationPins(activationCodeRole), false) + " TU Pins.", numberToAdd, referenceNumber);
                        Thread.Sleep(2000);
                        QueueOutbound("Congratulations! " + numberToAdd + " has been upgraded to " + activationCodeRole + ".", senderNumber, referenceNumber);
                    }
                    else {
                        QueueOutbound("Registration Error: Activation Code Already Used by " + GetActivationCodeUser(activationCode) + ".", senderNumber, referenceNumber);
                    }
                }
                else {
                    QueueOutbound("Additional Account Error: Invalid Activation Code.", senderNumber, referenceNumber);
                }
            }
            catch {
                QueueOutbound("Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
            }
        }

        private void ProcessStatusUpgrade(string senderNumber, string command, string referenceNumber) {

            try {

                string activationCode = command.Split(' ')[1].Trim();
                string upgradeRole = command.Split(' ')[0].Trim().Substring(2).ToUpper();

                string senderRole = GetUserRole(senderNumber);

                int senderRoleRank = GetRoleRank(senderRole);
                int upgradeRoleRank = GetRoleRank(upgradeRole);

                bool canUpgrade = senderRoleRank <= upgradeRoleRank;

                if ((upgradeRole == "DISTRIBUTOR") || (upgradeRole == "DEALER") || (upgradeRole == "MOBILE") || (upgradeRole == "CITY") || (upgradeRole == "PROVINCIAL")) {

                    if (ActivationCodeUsable(activationCode, upgradeRole)) {

                        if (GetActivationCodeUser(activationCode) == "") {

                            if (canUpgrade) {

                                string query = "UPDATE users SET Role = '" + upgradeRole + "', Pins = '" + (int.Parse(GetUserPins(senderNumber)) + int.Parse(GetNumberOfRegistrationPins(upgradeRole))).ToString() + "' WHERE PhoneNumber = '" + senderNumber + "'";
                                RunDatabaseQuery(query);

                                UseActivationCode(activationCode, GetUserUsername(senderNumber));
                                QueueOutbound("Congratulations! You have upgraded your account to " + upgradeRole + " status. You now have " + GetNumberOfRegistrationPins(upgradeRole) + " additional TU Pins.", senderNumber, referenceNumber);
                            }
                            else {
                                QueueOutbound("Upgrade Error: You can not downgrade or upgrade to your current status.", senderNumber, referenceNumber);
                            }
                        }
                        else {
                            QueueOutbound("Upgrade Error: Upgrade Code Already Used by " + GetActivationCodeUser(activationCode) + ".", senderNumber, referenceNumber);
                        }
                    }
                    else {
                        QueueOutbound("Upgrade Error: Invalid Upgrade Code.", senderNumber, referenceNumber);
                    }
                }
                else {
                    QueueOutbound("Upgrade Error: Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
                }
            }
            catch {
                QueueOutbound("Upgrade Error: Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
            }
        }
        #endregion

        #region THREAD - Process Outbounds
        private void ProcessOutbounds() {
            int SenderPortOneMessageCounter = 0;
            int SenderPortTwoMessageCounter = 0;

            while (true) {
                string query = "SELECT * FROM outbounds WHERE Status = 'PENDING'";

                DataTable dataTable = RunQueryDataTable(query);

                foreach (DataRow row in dataTable.Rows) {
                    string message = row["Message"].ToString();
                    string receiverNumber = row["ReceiverNumber"].ToString();
                    string referenceNumber = row["ReferenceNumber"].ToString();

                    if(SenderPortOneActive && SenderPortTwoActive) {
                        if (SenderPortOneMessageCounter <= SenderPortTwoMessageCounter) {
                            SendMessage(SenderOnePort, message, receiverNumber, referenceNumber);
                            
                            SenderPortOneMessageCounter++;
                        }
                        else {
                            SendMessage(SenderTwoPort, message, receiverNumber, referenceNumber);
                            SenderPortTwoMessageCounter++;
                        }
                    }
                    else if (SenderPortOneActive) {
                        SendMessage(SenderOnePort, message, receiverNumber, referenceNumber);
                        SenderPortOneMessageCounter++;
                    }
                    else if (SenderPortTwoActive) {
                        SendMessage(SenderTwoPort, message, receiverNumber, referenceNumber);
                        SenderPortTwoMessageCounter++;
                    }
                    else {
                        MessageBox.Show("WARNING! An outbound is pending but no sending port is active.");
                    }
                }

                Thread.Sleep(500);
            }
        }
        #endregion

        #region METHODS - User Manipulation
        private void RegisterUser(string username, string password, string phoneNumber, string fullName, string birthDate, string address, string role, string activatedBy) {

            int pins = int.Parse(GetNumberOfRegistrationPins(role));
            string query;

            query = "INSERT INTO users VALUES (null, '" + username + "', '" + password + "', '" + phoneNumber + "', '" + fullName + "', '" + birthDate + "', '" + address + "', '" + role + "', '" + activatedBy + "', 0, 0, '" + pins + "')";

            RunDatabaseQuery(query);
        } 

        private void DeductPin(string phoneNumber, string amount) {
            RunDatabaseQuery("UPDATE users SET Pins = (Pins - " + amount + ") WHERE PhoneNumber = '" + phoneNumber + "'");
        }

        #endregion

        #region METHODS - Messages Handling
        private void SendMessage(string port, string message, string recipient, string referenceNumber) {
            try {
                using (SerialPort smsPort = new SerialPort(port, 115200)) {

                    smsPort.Open();
                    Thread.Sleep(200);
                    smsPort.NewLine = Environment.NewLine;
                    smsPort.Write("AT\r\n");
                    Thread.Sleep(200);
                    smsPort.Write("AT+CMGF=1\r");
                    Thread.Sleep(200);
                    smsPort.Write("AT+CMGS=\"" + normalizeNumber(recipient) + "\"\r\n");
                    Thread.Sleep(200);
                    smsPort.Write(message + "\x1A");
                    Thread.Sleep(200);

                    string existing = smsPort.ReadExisting();

                    this.Invoke((MethodInvoker)delegate {
                        homeTextBoxSending.Text = homeTextBoxSending.Text + existing;
                        homeTextBoxSending.SelectionStart = homeTextBoxSending.Text.Length;
                        homeTextBoxSending.ScrollToCaret();
                    });
                    if (existing.Contains("AT+CMGS")) {
                        RunDatabaseQuery("UPDATE outbounds SET Status = 'DONE' WHERE referenceNumber = '" + referenceNumber + "'");
                    }

                    smsPort.Close();
                }
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }
        }

        private void QueueOutbound(string message, string receiverNumber, string referenceNumber) {
            RunDatabaseQuery("INSERT INTO outbounds (Message, ReceiverNumber, Status, DateTime, ReferenceNumber) VALUES ('" + message + "', '" + receiverNumber + "', 'PENDING', '" + DateTime.Now.ToString("yyyy-MM-dd h:mm:ss") + "', '" + referenceNumber + "')");
        }
        #endregion

        #region METHODS - Database Handling
        private void RunDatabaseQuery(string query) {
            MySqlConnection databaseConnection = new MySqlConnection(this.MySQLConnectionString);
            MySqlCommand commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();

            commandDatabase.ExecuteNonQuery();

            databaseConnection.Close();
        }

        private DataTable RunQueryDataTable(string query) {
            DataTable dataTable = new DataTable();

            MySqlConnection databaseConnection = new MySqlConnection(this.MySQLConnectionString);
            MySqlCommand commandDatabase = new MySqlCommand(query, databaseConnection);

            using (MySqlDataAdapter dataAdapter = new MySqlDataAdapter(commandDatabase)) {
                dataAdapter.Fill(dataTable);
            }

            return dataTable;
        }
        #endregion

        #region METHODS - Utility
        private string normalizeNumber(string number) {
            if (number.StartsWith("+")) {
                number = "0" + number.Substring(3);
            }
            else if (number.StartsWith("63")) {
                number = "0" + number.Substring(2);
            }

            return number;
        }

        private string GenerateReferenceNumber() {
            Random random = new Random();
            bool notUnique = true;

            string referenceNumber = "";
            string query;

            while (notUnique) {
                referenceNumber = DateTime.Now.ToString("MMddyyyyhmm") + "-" + random.Next(0, 10000) + "-" + random.Next(0, 10000);

                query = "SELECT COUNT(ReferenceNumber) FROM commands WHERE ReferenceNumber = '" + referenceNumber + "'";

                MySqlConnection databaseConnection = new MySqlConnection(this.MySQLConnectionString);
                MySqlCommand commandDatabase = new MySqlCommand(query, databaseConnection);

                databaseConnection.Open();

                MySqlDataReader myReader = commandDatabase.ExecuteReader();

                try {
                    myReader.Read();
                    if (myReader["COUNT(ReferenceNumber)"].ToString() != "1") {
                        notUnique = false;
                    }
                }
                catch (Exception error) {
                    MessageBox.Show(error.Message);
                }
            }

            return referenceNumber;
        }

        private bool UserExists(string number) {
            bool userExists = false;
            string query = "SELECT COUNT(PhoneNumber) FROM users WHERE PhoneNumber = '" + number + "'";

            MySqlConnection databaseConnection = new MySqlConnection(this.MySQLConnectionString);
            MySqlCommand commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();

            MySqlDataReader myReader = commandDatabase.ExecuteReader();

            try {
                myReader.Read();
                if (myReader["COUNT(PhoneNumber)"].ToString() == "1") {
                    userExists = true;
                }
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            return userExists;
        }

        private bool IsCharactersOnly(string input) {
            return Regex.IsMatch(input, @"^[a-zA-Z]+$");
        }

        private string FormatNumberWithComma(string number, bool withTrailingZeroes) {

            if (withTrailingZeroes) {
                number = String.Format("{0:n}", double.Parse(number));
            }
            else {
                number = String.Format("{0:n0}", double.Parse(number));
            }

            return number;
        }

        private bool UsernameValid(string username) {
            if((Regex.IsMatch(username, @"^[a-zA-Z0-9]+$")) && (username.Length <= 8)) {
                return true;
            }
            else {
                return false;
            }
        }

        private bool PasswordValid(string password) {
            if((password.Length == 5) && (Regex.IsMatch(password, @"^[0-9]+$"))) {
                return true;
            }
            else {
                return false;
            }
        }

        private bool DateValid(string date) {
            string month = date.Split('-')[0];
            string day = date.Split('-')[1];
            string year = date.Split('-')[2];

            bool validDate = (((int.Parse(month) <= 12) && (int.Parse(month) > 0)) && (year.Length == 4)) && ((int.Parse(day) > 0) && (int.Parse(day) <= 31));

            return validDate;
        }

        private bool ActivationCodeUsable(string code, string role) {

            bool activationCodeUsable = false;

            string query = "SELECT COUNT(Code) FROM activation_codes WHERE Code = '" + code + "' AND Role = '" + role + "'";

            MySqlConnection databaseConnection = new MySqlConnection(this.MySQLConnectionString);
            MySqlCommand commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();

            MySqlDataReader myReader = commandDatabase.ExecuteReader();

            try {
                myReader.Read();
                if (myReader["COUNT(Code)"].ToString() == "1") {
                    activationCodeUsable = true;
                }
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            return activationCodeUsable;
        }

        private void UseActivationCode(string code, string username) {
            RunDatabaseQuery("UPDATE activation_codes SET Status = 'USED', UsedBy = '" + username + "' WHERE Code = '" + code + "'");
        }

        private string GetNumberOfRegistrationPins(string role) {

            string query = "";
            string pins = "";

            if (role == "RETAILER") {
                query = "SELECT Value FROM configurations WHERE Parameter = 'RetailerPins'";
            }
            else if (role == "DISTRIBUTOR") {
                query = "SELECT Value FROM configurations WHERE Parameter = 'DistributorPins'";
            }
            else if (role == "DEALER") {
                query = "SELECT Value FROM configurations WHERE Parameter = 'DealerPins'";
            }
            else if (role == "MOBILE") {
                query = "SELECT Value FROM configurations WHERE Parameter = 'MobilePins'";
            }
            else if (role == "CITY") {
                query = "SELECT Value FROM configurations WHERE Parameter = 'CityPins'";
            }
            else if (role == "PROVINCIAL") {
                query = "SELECT Value FROM configurations WHERE Parameter = 'ProvincialPins'";
            }

            DataTable dataTableBalance = RunQueryDataTable(query);

            try {
                pins = dataTableBalance.Rows[0]["Value"].ToString();
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            return pins;
        }

        private string GetActivationCodeUser(string code) {

            string query = "SELECT UsedBy FROM activation_codes WHERE Code = '" + code + "'";
            string usedBy = "";

            DataTable dataTableBalance = RunQueryDataTable(query);

            try {
                usedBy = dataTableBalance.Rows[0]["UsedBy"].ToString();
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            return usedBy;
        }

        private string GetUserPins(string phoneNumber) {

            string query = "SELECT Pins FROM users WHERE PhoneNumber = '" + phoneNumber + "'";
            string pins = "";

            DataTable dataTableBalance = RunQueryDataTable(query);

            try {
                pins = dataTableBalance.Rows[0]["Pins"].ToString();
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            return pins;
        }

        private string GetUserRole(string phoneNumber) {
            string query = "SELECT Role FROM users WHERE PhoneNumber = '" + phoneNumber + "'";
            string role = "";

            DataTable dataTableBalance = RunQueryDataTable(query);

            try {
                role = dataTableBalance.Rows[0]["Role"].ToString();
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            return role;
        }

        private string GetActivationCodeRole(string activationCode) {
            string query = "SELECT Role FROM activation_codes WHERE Code = '" + activationCode + "'";
            string role = "";

            DataTable dataTableBalance = RunQueryDataTable(query);

            try {
                role = dataTableBalance.Rows[0]["Role"].ToString();
            }
            catch (Exception error) {
                MessageBox.Show(error.Message + "GetActivationCodeRole");
            }

            return role;
        }

        private string GetUserUsername(string phoneNumber) {
            string query = "SELECT Username FROM users WHERE PhoneNumber = '" + phoneNumber + "'";
            string username = "";

            DataTable dataTableBalance = RunQueryDataTable(query);

            try {
                username = dataTableBalance.Rows[0]["Username"].ToString();
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            return username;
        }

        private int GetRoleRank(string role) {
            
            string query = "SELECT Value FROM configurations WHERE Parameter = '" + char.ToUpper(role.ToLower()[0]) + role.ToLower().Substring(1) + "Rank'";
            int rank = 0;

            DataTable dataTableBalance = RunQueryDataTable(query);

            if(dataTableBalance.Rows.Count > 0) {
                rank = int.Parse(dataTableBalance.Rows[0]["Value"].ToString());
            }

            return rank;
        }
        
        private bool CanTransfer(string senderNumber, string receiverNumber, string type) {

            bool canTransfer = false;

            string senderRole = GetUserRole(senderNumber);
            string receiverRole = GetUserRole(receiverNumber);

            int senderRank = GetRoleRank(senderRole);
            int receiverRank = GetRoleRank(receiverRole);

            if (type == "TTU") {
                if (senderRank == 0) {
                    canTransfer = false;
                }
                else if (senderRank >= receiverRank) {
                    canTransfer = true;
                }
                else {
                    canTransfer = false;
                }
            }
            else {
                if(senderRank >= receiverRank) {
                    canTransfer = true;
                }
            }
            return canTransfer;
        }
        #endregion

        #region METHODS - Ports
        private void InitializePorts() {

            //GATEWAY ONE PORT ASSIGNING
            string query = "SELECT Value FROM configurations WHERE Parameter = 'GateWayOne'";

            MySqlConnection databaseConnection = new MySqlConnection(this.MySQLConnectionString);
            MySqlCommand commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();

            MySqlDataReader myReader = commandDatabase.ExecuteReader();

            try {
                myReader.Read();
                this.GateWayOnePort = myReader["Value"].ToString();
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            databaseConnection.Close();


            //GATEWAY ONE PORT ASSIGNING
            query = "SELECT Value FROM configurations WHERE Parameter = 'GateWayTwo'";
            commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();

            myReader = commandDatabase.ExecuteReader();

            try {
                myReader.Read();
                this.GateWayTwoPort = myReader["Value"].ToString();
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            databaseConnection.Close();

            //SENDER ONE PORT ASSIGNING
            query = "SELECT Value FROM configurations WHERE Parameter = 'SenderOnePort'";
            commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();

            myReader = commandDatabase.ExecuteReader();

            try {
                myReader.Read();
                this.SenderOnePort = myReader["Value"].ToString();
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            databaseConnection.Close();

            //SENDER TWO PORT ASSIGNING
            query = "SELECT Value FROM configurations WHERE Parameter = 'SenderTwoPort'";
            commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();

            myReader = commandDatabase.ExecuteReader();

            try {
                myReader.Read();
                this.SenderTwoPort = myReader["Value"].ToString();
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }

            databaseConnection.Close();

            //MessageBox.Show("GateWayone = " + this.GateWayOnePort);
            //MessageBox.Show("GateWaytwoPort = " + this.GateWayTwoPort);
            //MessageBox.Show("Sender one = " + this.SenderOnePort);
            //MessageBox.Show("sender two = " + this.SenderTwoPort);
        }

        private void ClearMessagesOnGateWay(string port) {
            try {
                using (SerialPort serialPort = new SerialPort(port, 115200)) {
                    serialPort.Open();
                    serialPort.NewLine = Environment.NewLine;

                    serialPort.Write("AT\r\n");
                    Thread.Sleep(100);
                    serialPort.Write("AT+CMGF=1\r\n");
                    Thread.Sleep(100);
                    serialPort.Write("AT+CMGD=1,4\r\n");
                    Thread.Sleep(100);
                }
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }
        }

        private void TestPorts() {
            int i = 11;
            while (true) {
                string port = "COM" + i.ToString();

                try {
                    using (SerialPort sp = new SerialPort(port, 115200)) {
                        sp.Open();
                        sp.Write("AT+CNMI=1,2,0,0,0\r");
                        Thread.Sleep(100);
                        sp.NewLine = Environment.NewLine;
                        sp.Write("AT\r\n");
                        Thread.Sleep(100);
                        sp.Write("AT+CNUM\r");
                        Thread.Sleep(100);
                        sp.Write("AT\r\n");
                        Thread.Sleep(100);
                        sp.Write("AT+CMGF=1\r\n");
                        Thread.Sleep(100);
                        sp.Write("AT+CMGL=\"ALL\"\r\n");
                        Thread.Sleep(100);
                        string existing = sp.ReadExisting();
                        MessageBox.Show(existing.Split(':')[1].Split('"')[3] + " " + port);
                        sp.Close();
                        sp.Dispose();
                    }
                }
                catch (Exception ex) {
                    if (!ex.Message.Contains("bounds"))
                        MessageBox.Show(ex.Message);
                    else
                        MessageBox.Show("PORT EMPTY");
                }


                if (i == 18) {
                    i = 11;
                }

                i++;
            }
        }

        #endregion

        #region GRAPHICAL USER INTERFACE
        private void InitializeGUI() {
            homeRadioButtonOnGlobeOne.Checked = true;
            homeRadioButtonOnGlobeTwo.Checked = true;
            homeRadioButtonOnGlobeThree.Checked = true;
            homeRadioButtonOnSmart.Checked = true;

            homeRadioButtonOnSenderOne.Checked = true;
            homeRadioButtonOnSenderTwo.Checked = true;

            homeGlobeOneStatus.Text = "Running...";
            homeGlobeOneStatus.BackColor = Color.GreenYellow;

            homeGlobeTwoStatus.BackColor = Color.GreenYellow;
            homeGlobeTwoStatus.Text = "Running...";

            homeGlobeThreeStatus.BackColor = Color.GreenYellow;
            homeGlobeThreeStatus.Text = "Running...";

            homeSmartStatus.BackColor = Color.GreenYellow;
            homeSmartStatus.Text = "Running...";

            homeSenderOneStatus.BackColor = Color.GreenYellow;
            homeSenderOneStatus.Text = "Running...";

            homeSenderTwoStatus.BackColor = Color.GreenYellow;
            homeSenderTwoStatus.Text = "Running...";
        }

        private void homeRadioButtonOffGlobeOne_CheckedChanged(object sender, EventArgs e) {
            if (homeRadioButtonOnGlobeOne.Checked) {
                GlobePortOneActive = true;

                homeGlobeOneStatus.BackColor = Color.GreenYellow;
                homeGlobeOneStatus.Text = "Running...";

                MessageBox.Show("GLOBE Port One has been activated");
            }
            else {
                GlobePortOneActive = false;

                homeGlobeOneStatus.BackColor = Color.PaleVioletRed;
                homeGlobeOneStatus.Text = "STOPPED";

                MessageBox.Show("GLOBE Port One has been deactivated");
            }
        }

        private void homeRadioButtonOffGlobeTwo_CheckedChanged(object sender, EventArgs e) {
            if (homeRadioButtonOnGlobeTwo.Checked) {
                GlobePortTwoActive = true;

                homeGlobeTwoStatus.BackColor = Color.GreenYellow;
                homeGlobeTwoStatus.Text = "Running...";

                MessageBox.Show("GLOBE Port Two has been Activated");
            }
            else {
                GlobePortTwoActive = false;

                homeGlobeTwoStatus.BackColor = Color.PaleVioletRed;
                homeGlobeTwoStatus.Text = "STOPPED";

                MessageBox.Show("GLOBE Port Two has been Deactivated");
            }
        }

        private void homeRadioButtonOffGlobeThree_CheckedChanged(object sender, EventArgs e) {
            if (homeRadioButtonOnGlobeThree.Checked) {
                GlobePortThreeActive = true;

                homeGlobeThreeStatus.BackColor = Color.GreenYellow;
                homeGlobeThreeStatus.Text = "Running...";

                MessageBox.Show("GLOBE Port Three has been Activated");
            }
            else {
                GlobePortThreeActive = false;

                homeGlobeThreeStatus.BackColor = Color.PaleVioletRed;
                homeGlobeThreeStatus.Text = "STOPPED";

                MessageBox.Show("GLOBE Port Three has been Deactivated");
            }
        }

        private void homeRadioButtonOffSmart_CheckedChanged(object sender, EventArgs e) {
            if (homeRadioButtonOnSmart.Checked) {
                SmartPortActive = true;

                homeSmartStatus.BackColor = Color.GreenYellow;
                homeSmartStatus.Text = "Running...";

                MessageBox.Show("SMART Port has been Activated");
            }
            else {
                SmartPortActive = false;

                homeSmartStatus.BackColor = Color.PaleVioletRed;
                homeSmartStatus.Text = "STOPPED";

                MessageBox.Show("SMART Port has been Deactivated");
            }
        }

        private void homeRadioButtonOffSenderOne_CheckedChanged(object sender, EventArgs e) {
            if (homeRadioButtonOnSenderOne.Checked) {
                SenderPortOneActive = true;

                homeSenderOneStatus.BackColor = Color.GreenYellow;
                homeSenderOneStatus.Text = "Running...";

                MessageBox.Show("SMS Sender One has been Activated");
            }
            else {
                SenderPortOneActive = false;

                homeSenderOneStatus.BackColor = Color.PaleVioletRed;
                homeSenderOneStatus.Text = "STOPPED";

                MessageBox.Show("SMS Sender One has been Deactivated");
            }
        }

        private void homeRadioButtonOffSenderTwo_CheckedChanged(object sender, EventArgs e) {
            if (homeRadioButtonOnSenderTwo.Checked) {
                SenderPortTwoActive = true;

                homeSenderTwoStatus.BackColor = Color.GreenYellow;
                homeSenderTwoStatus.Text = "Running...";

                MessageBox.Show("SMS Sender Two has been Activated");
            }
            else {
                SenderPortTwoActive = false;

                homeSenderTwoStatus.BackColor = Color.PaleVioletRed;
                homeSenderTwoStatus.Text = "STOPPED";

                MessageBox.Show("SMS Sender Two has been Deactivated");
            }
        }
        #endregion
    }
}
