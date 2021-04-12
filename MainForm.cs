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
using System.Media;
using System.Data.SqlClient;

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

            Thread processSmartLoadingQueueThread = new Thread(ProcessSmartLoadingQueue);
            Thread processGlobeLoadingQueueThread = new Thread(ProcessGlobeLoadingQueue);

            receiveMessageGateWayOneThread.Start();
            receiveMessageGateWayTwoThread.Start();

            processCommandsThread.Start();
            processOutboundsThread.Start();

            processSmartLoadingQueueThread.Start();
            processGlobeLoadingQueueThread.Start();

            LoadSettingsValues();
            //ClearMessagesOnGateWay(GateWayOnePort);
            //ClearMessagesOnGateWay(GateWayTwoPort);
            //ClearMessagesOnGateWay(SenderOnePort);
            //ClearMessagesOnGateWay(SenderTwoPort);
            //ClearMessagesOnGateWay(GlobeRetailerOnePort);
            //ClearMessagesOnGateWay(GlobeRetailerTwoPort);
            //ClearMessagesOnGateWay(GlobeRetailerThreePort);
            //ClearMessagesOnGateWay(SmartRetailerPort);
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

        public bool SmartRetailerIsSending = false;
        public bool GlobeOnePortDialingUSSD = false;
        public bool GlobeTwoPortDialingUSSD = false;
        public bool GlobeThreePortDialingUSSD = false;
        #endregion

        #region THREAD - Receive Messages on Gateways
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

                            if(commandSent != "") {

                                if (IsCharactersOnly(commandType)) {

                                    commandType.ToUpper();

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
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }
        }

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

                            if(commandSent != "") {

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
                    else if (commandType == "TLC") {

                        string numberToReceive = command.Split(' ')[1].Trim();
                        string amount = command.Split(' ')[2].Trim();

                        DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'RepeatTransactionMinutes'");
                        string repeatTransactionMinutes = configurationsTable.Rows[0]["Value"].ToString();

                        if (!HasSameTransactionWithin30Minutes(senderNumber, numberToReceive, amount, "TLC")) {
                            ProcessTransferLoadCredit(senderNumber, command, referenceNumber);
                        }
                        else {
                            QueueOutbound("TLC Error: Transaction can only be repeated after " + repeatTransactionMinutes + " minutes.", senderNumber, referenceNumber);
                        }
                    }
                    else if (commandType == "TTU") {

                        string receiverNumber = command.Split(' ')[1];
                        string amount = command.Split(' ')[2];

                        DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'RepeatTransactionMinutes'");
                        string repeatTransactionMinutes = configurationsTable.Rows[0]["Value"].ToString();

                        if (!HasSameTransactionWithin30Minutes(senderNumber, receiverNumber, amount, "TTU")) {
                            ProcessTransferPins(senderNumber, command, referenceNumber);
                        }
                        else {
                            QueueOutbound("TTU Error: Transaction can only be repeated after " + repeatTransactionMinutes + " minutes.", senderNumber, referenceNumber);
                        }
                        
                    }
                    else if (commandType == "LOAD") {

                        string receiverNumber = command.Split(' ')[0];
                        string amount = command.Split(' ')[1];

                        DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'RepeatTransactionMinutes'");
                        string repeatTransactionMinutes = configurationsTable.Rows[0]["Value"].ToString();

                        if(!HasSameTransactionWithin30Minutes(senderNumber, receiverNumber, amount, "LOAD")) {
                            ProcessLoadingQueue(senderNumber, command, referenceNumber);
                        }
                        else {
                            QueueOutbound("Load Error: Transaction can only be repeated after " + repeatTransactionMinutes + " minutes.", senderNumber, referenceNumber);
                        }
                    }
                    else if (commandType == "CPW") {
                        ProcessChangePassword(senderNumber, command, referenceNumber);
                    }
                    else if (commandType == "HELP") {
                        ProcessHelp(senderNumber, command, referenceNumber);
                    }
                    else if (commandType == "LOGIN") {

                        string receiverNumber = command.Split(' ')[1];
                        string amount = command.Split(' ')[2];

                        DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'RepeatTransactionMinutes'");
                        string repeatTransactionMinutes = configurationsTable.Rows[0]["Value"].ToString();

                        if (!HasSameTransactionWithin30Minutes(senderNumber, receiverNumber, amount, "LOAD")) {
                            ProcessLogin(senderNumber, command, referenceNumber);
                        }
                        else {
                            QueueOutbound("Login Error: Transaction can only be repeated after " + repeatTransactionMinutes + " minutes.", senderNumber, referenceNumber);
                        }
                    }
                    else if (commandType == "TV") {
                        ProcessGSAT(senderNumber, command, referenceNumber);
                    }
                    else if (commandType == "REP TLC") {
                        command = command.Replace("REP", "").Trim();

                        ProcessTransferLoadCredit(senderNumber, command, referenceNumber);
                    }
                    else if (commandType == "REP TTU") {
                        command = command.Replace("REP", "").Trim();

                        ProcessTransferPins(senderNumber, command, referenceNumber);
                    }
                    else if (commandType == "REP LOAD") {
                        command = command.Replace("REP", "").Trim();

                        ProcessLoadingQueue(senderNumber, command, referenceNumber);
                    }
                    else if (commandType == "REP LOGIN") {
                        command = command.Replace("REP", "").Trim();

                        ProcessLogin(senderNumber, command, referenceNumber);
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

                                DeductPin(senderNumber, "1");

                                message = numberToRegister + " has been registered as a Techno User. Remaining TU Pins: " + GetUserPins(senderNumber);
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
            catch (Exception error){
                MessageBox.Show(error.Message);
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

                                            message = "Congratulations! " + numberToRegister + " has been registered as a " + role + ".";
                                            QueueOutbound(message, senderNumber, referenceNumber);

                                            Thread.Sleep(2000);

                                            message = "Congratulations! You are now registered as " + role + ". With USERNAME: " + username + " and PW: " + password + ". NEVER share this information to anyone. You may refer to the product guide for transactions. You now have " + GetNumberOfRegistrationPins(role) + " pins";
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

        private void ProcessTransferLoadCredit(string senderNumber, string command, string referenceNumber) {
            
            try {

                string numberToReceive = command.Split(' ')[1].Trim();
                string amount = command.Split(' ')[2].Trim();

                DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'MinimumTLC'");
                double minimumTransferAmount = double.Parse(configurationsTable.Rows[0]["Value"].ToString());

                configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'RepeatTransactionMinutes'");
                string repeatTransactionMinutes = configurationsTable.Rows[0]["Value"].ToString();

                double amountToBeDeducted, income;
                string newSenderBalance, newReceiverBalance;

                if (UserExists(numberToReceive)) {

                    if (CanTransfer(senderNumber, numberToReceive, "TLC")) {

                        if (senderNumber != numberToReceive) {

                            if (double.Parse(amount) >= minimumTransferAmount) {

                                amountToBeDeducted = (double.Parse(amount)) * (1 - GetPercentOfIncomeTLC(senderNumber, numberToReceive));
                                income = double.Parse(amount) - amountToBeDeducted;

                                if (HasLoadCreditBalance(senderNumber, amountToBeDeducted.ToString())) {


                                    newSenderBalance = (double.Parse(GetUserBalance(senderNumber)) - amountToBeDeducted).ToString();
                                    newReceiverBalance = (double.Parse(GetUserBalance(numberToReceive)) + double.Parse(amount)).ToString();

                                    RunDatabaseQuery("UPDATE users SET Balance = '" + newReceiverBalance + "' WHERE PhoneNumber = '" + numberToReceive + "'");
                                    RunDatabaseQuery("UPDATE users SET Balance = '" + newSenderBalance + "' WHERE PhoneNumber = '" + senderNumber + "'");

                                    RunDatabaseQuery("INSERT INTO history_income (PhoneNumber, Income, Type, DateTime, ReferenceNumber) VALUES ('" + senderNumber + "', '" + income + "', 'TLC', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "', '" + referenceNumber + "')");

                                    RunDatabaseQuery("INSERT INTO history_tlc (SenderNumber, Amount, ReceiverNumber, DateTime, ReferenceNumber) VALUES ('" + senderNumber + "', '" + amount + "(" + amountToBeDeducted + ")', '" + numberToReceive + "', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "', '" + referenceNumber + "')");

                                    QueueOutbound("(1/2) You have issued P" + FormatNumberWithComma(amount, true) + "(" + FormatNumberWithComma(amountToBeDeducted.ToString(), true) + ") load credits to " + numberToReceive + ". New load wallet balance: P" + newSenderBalance + ".", senderNumber, referenceNumber);
                                    Thread.Sleep(2000);
                                    QueueOutbound("(1/2) You have received P" + FormatNumberWithComma(amount, true) + " load credits from " + senderNumber + ". New load credit balance: P" + FormatNumberWithComma(newReceiverBalance, true) + ". ", numberToReceive, referenceNumber);
                                    
                                
                                    Thread.Sleep(2000);
                                    QueueOutbound("(2/2) RefNo: " + referenceNumber + ". " + "Date: " + DateTime.Now.ToString(), senderNumber, referenceNumber);
                                    Thread.Sleep(2000);
                                    QueueOutbound("(2/2) RefNo: " + referenceNumber + ". " + "Date: " + DateTime.Now.ToString(), numberToReceive, referenceNumber);
                                }
                                else {
                                    QueueOutbound("TLC Error: You have insufficient balance to complete the transaction.", senderNumber, referenceNumber);
                                }
                            }
                            else {
                                QueueOutbound("TLC Error: The minimum amount that can be transferred is P" + FormatNumberWithComma(minimumTransferAmount.ToString(), true) + "", senderNumber, referenceNumber);
                            }
                        }
                        else {
                            QueueOutbound("TLC Error: You can not transfer to your own account.", senderNumber, referenceNumber);
                        }
                    }
                    else {
                        QueueOutbound("TLC Error: You are not allowed to transfer to this account.", senderNumber, referenceNumber);
                    }
                }
                else {
                    QueueOutbound("TLC Error: Receiver is not registered in the system.", senderNumber, referenceNumber);
                }
            }
            catch {
                QueueOutbound("TLC Error: Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
            }
        }
  
        private void ProcessTransferPins(string senderNumber, string command, string referenceNumber) {

            try {

                string receiverNumber = command.Split(' ')[1];
                string amount = command.Split(' ')[2];

                DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'MinimumTTU'");
                int minimumPinAmount = int.Parse(configurationsTable.Rows[0]["Value"].ToString());

                configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'RepeatTransactionMinutes'");
                string repeatTransactionMinutes = configurationsTable.Rows[0]["Value"].ToString();

                if (UserExists(receiverNumber)) {

                    if (CanTransfer(senderNumber, receiverNumber, "TTU")) {

                        if (senderNumber != receiverNumber) {

                            if (int.Parse(amount) >= minimumPinAmount) {

                                if (HasPinBalance(senderNumber, int.Parse(amount))) {

                                    RunDatabaseQuery("INSERT INTO history_ttu (SenderNumber, Amount, ReceiverNumber, DateTime, ReferenceNumber) VALUES ('" + senderNumber + "', '" + amount + "', '" + receiverNumber + "', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "', '" + referenceNumber + "')");
                                    RunDatabaseQuery("UPDATE users SET Pins = Pins - " + amount + " WHERE PhoneNumber = '" + senderNumber + "'");
                                    RunDatabaseQuery("UPDATE users SET Pins = Pins + " + amount + " WHERE PhoneNumber = '" + receiverNumber + "'");

                                    QueueOutbound("You have issued " + amount + " Techno User Pin/s to " + receiverNumber + ". Available TU Pins: " + GetUserPins(senderNumber) + ". RefNo: " + referenceNumber + Environment.NewLine + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), senderNumber, referenceNumber);
                                    Thread.Sleep(2000);
                                    QueueOutbound("You have received " + amount + " Techno User Pin/s from " + senderNumber + ". Available TU Pins: " + GetUserPins(receiverNumber) + ". RefNo: " + referenceNumber + Environment.NewLine + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), receiverNumber, referenceNumber);
                                }
                                else {
                                    QueueOutbound("TTU Error: You have insufficient pins to complete the transaction.", senderNumber, referenceNumber);
                                }
                            }
                            else {
                                QueueOutbound("TTU Error: The minimum amount of Pin that can be transferred is " + FormatNumberWithComma(minimumPinAmount.ToString(), false) + " TUP.", senderNumber, referenceNumber);
                            }
                        }
                        else {
                            QueueOutbound("TTU Error: You can not transfer to your own account.", senderNumber, referenceNumber);
                        }
                    }
                    else {
                        QueueOutbound("TTU Error: You are not allowed to transfer to this account.", senderNumber, referenceNumber);
                    }
                }
                else {
                    QueueOutbound("TTU Error: Receiver is not registered in the system.", senderNumber, referenceNumber);
                }
            }
            catch {
                QueueOutbound("TTU Error: Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
            }
        }

        private void ProcessLoadingQueue(string senderNumber, string command, string referenceNumber) {

            try {
                string receiverNumber = command.Split(' ')[0].Trim();
                string productCode = command.Split(' ')[1].Trim();

                string receiverCarrier = GetCarrier(receiverNumber);

                if(receiverCarrier == "") {
                    if(receiverNumber.Length == 11) {
                        receiverCarrier = "PLDT";
                    }
                    else {
                        receiverCarrier = "CIGNAL";
                    }
                }

                double incomePercentage, income;

                DataTable configurationsTable;
                DataTable productCodesTable; 

                double productPrice;

                if (ProductCodeExists(productCode, receiverCarrier)) {

                    string query = "INSERT INTO loads (SenderNumber, ProductCode, ReceiverNumber, Carrier, Status, ReferenceNumber, DateTime) VALUES ('" + senderNumber + "', '" + productCode + "', '" + receiverNumber + "', '" + receiverCarrier + "', 'PENDING', '" + referenceNumber + "', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')";

                    productCodesTable = RunQueryDataTable("SELECT * FROM product_codes WHERE ProductCode = '" + productCode + "' AND Carrier = '" + receiverCarrier + "'");

                    productPrice = double.Parse(productCodesTable.Rows[0]["Price"].ToString());

                    if ((receiverCarrier == "SMART") || (receiverCarrier == "TNT") || (receiverCarrier == "PLDT") || (receiverCarrier == "CIGNAL")) {

                        configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'SmartLoadPercentage'");

                        incomePercentage = double.Parse(configurationsTable.Rows[0]["Value"].ToString());

                        income = productPrice * incomePercentage;

                        if (HasLoadCreditBalance(senderNumber, (productPrice - income).ToString())) {
                            if (SmartPortActive) {
                                RunDatabaseQuery(query);

                                //DEDUCT FROM LOAD WALLET
                                string amount = productCodesTable.Rows[0]["Price"].ToString();
                                string systemResponse = "(1/2) You have successfully loaded " + productCode + "(" + (double.Parse(amount) * (1 - GetPercentIncomeLoad("SMART"))).ToString() + ") to " + receiverNumber + ". RefNo: " + referenceNumber + Environment.NewLine + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                                QueueOutbound(systemResponse, senderNumber, referenceNumber);
                                Thread.Sleep(2000);
                                QueueOutbound("(2/2) New load wallet balance: P" + GetUserBalance(senderNumber), senderNumber, referenceNumber);
                                Thread.Sleep(2000);

                                DeductLoadCredit(senderNumber, (double.Parse(amount) * (1 - GetPercentIncomeLoad("SMART"))).ToString());
                                RunDatabaseQuery("INSERT INTO history_income (PhoneNumber, Income, Type, DateTime, ReferenceNumber) VALUES ('" + senderNumber + "', '" + (double.Parse(amount) * GetPercentIncomeLoad("SMART")).ToString() + "', 'LOAD', '" + GetCurrentDateTime() + "', '" + referenceNumber + "')");
                                amount = amount + "(" + (double.Parse(amount) * (1 - GetPercentIncomeLoad("SMART"))).ToString() + ")";
                                
                                RunDatabaseQuery("INSERT INTO history_load (SenderNumber, Amount, ReceiverNumber, DateTime, ReferenceNumber, SystemResponse) VALUES ('" + senderNumber + "', '" + amount + "', '" + receiverNumber + "', '" + GetCurrentDateTime() + "', '" + referenceNumber + "', '" + systemResponse + "')");
                            }
                            else {
                                QueueOutbound("Loading Error: Smart/TNT loading is currently unavailable. Please try again later.", senderNumber, referenceNumber);
                            }
                        }
                        else {
                            QueueOutbound("Loading Error: You have insufficient balance to complete the transaction.", senderNumber, referenceNumber);
                        }
                    }
                    else if ((receiverCarrier == "GLOBE") || (receiverCarrier == "TM")) {

                        configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'GlobeLoadPercentage'");

                        incomePercentage = double.Parse(configurationsTable.Rows[0]["Value"].ToString());

                        income = productPrice * incomePercentage;

                        if (HasLoadCreditBalance(senderNumber, (productPrice - income).ToString())) {
                            if (GlobePortOneActive || GlobePortTwoActive || GlobePortThreeActive) {
                                RunDatabaseQuery(query);

                                //DEDUCT FROM LOAD WALLET
                                string amount = productCodesTable.Rows[0]["Price"].ToString();
                                string systemResponse = "(1/2) You have successfully loaded " + productCode + "(" + (double.Parse(amount) * (1 - GetPercentIncomeLoad("GLOBE"))).ToString() + ") to " + receiverNumber + ". RefNo: " + referenceNumber + Environment.NewLine + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                                QueueOutbound(systemResponse, senderNumber, referenceNumber);
                                Thread.Sleep(2000);
                                QueueOutbound("(2/2) New load wallet balance: P" + GetUserBalance(senderNumber), senderNumber, referenceNumber);
                                Thread.Sleep(2000);

                                DeductLoadCredit(senderNumber, (double.Parse(amount) * (1 - GetPercentIncomeLoad("GLOBE"))).ToString());
                                RunDatabaseQuery("INSERT INTO history_income (PhoneNumber, Income, Type, DateTime, ReferenceNumber) VALUES ('" + senderNumber + "', '" + (double.Parse(amount) * GetPercentIncomeLoad("GLOBE")).ToString() + "', 'LOAD', '" + GetCurrentDateTime() + "', '" + referenceNumber + "')");
                                amount = amount + "(" + (double.Parse(amount) * (1 - GetPercentIncomeLoad("GLOBE"))).ToString() + ")";

                                RunDatabaseQuery("INSERT INTO history_load (SenderNumber, Amount, ReceiverNumber, DateTime, ReferenceNumber, SystemResponse) VALUES ('" + senderNumber + "', '" + amount + "', '" + receiverNumber + "', '" + GetCurrentDateTime() + "', '" + referenceNumber + "', '" + systemResponse + "')");
                            }
                            else {
                                QueueOutbound("Loading Error: Globe/TM loading is currently unavailable. Please try again later.", senderNumber, referenceNumber);
                            }
                        }
                        else {
                            QueueOutbound("Loading Error: You have insufficient balance to complete the transaction.", senderNumber, referenceNumber);
                        }
                    }
                }
                else {
                    QueueOutbound("Loading Error: Invalid Product Code. Please use the correct product code", senderNumber, referenceNumber);
                }
            }
            catch (Exception error){
                MessageBox.Show(error.Message);
                QueueOutbound("Loading Error: Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
            }
        }

        private void ProcessChangePassword(string senderNumber, string command, string referenceNumber) {

            try {

                string username = command.Split(' ')[1].Split('/')[0];
                string oldPassword = command.Split(' ')[1].Split('/')[1].Trim();
                string newPassword = command.Split(' ')[1].Split('/')[2].Trim();

                if(username == GetUserUsername(senderNumber)) {

                    if (oldPassword == GetUserPassword(senderNumber)) {

                        if (PasswordValid(newPassword)) {

                            RunDatabaseQuery("UPDATE users SET Password = '" + newPassword + "' WHERE PhoneNumber = '" + senderNumber + "'");
                            QueueOutbound("Change Password Successful! Never share your username and password to anyone.", senderNumber, referenceNumber);
                            Thread.Sleep(2000);
                        }
                        else {
                            QueueOutbound("Change Password Error: New Password Must Be 5-Digits.", senderNumber, referenceNumber);
                        }
                    }
                    else {
                        QueueOutbound("Change Password Error: Invalid Old Password.", senderNumber, referenceNumber);
                    }
                }
                else {
                    QueueOutbound("Change Password Error: Invalid Username. Make sure you have the correct username.", senderNumber, referenceNumber);
                }
            }
            catch {
                QueueOutbound("Change Password Error: Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
            }
        }

        private void ProcessHelp(string senderNumber, string command, string referenceNumber) {

            try {

                string helpReferenceNumber = command.Split(' ')[1].Trim().Split('/')[0].Trim();
                string dateOfIncident = command.Split(' ')[1].Trim().Split('/')[1].Trim();
                string code = command.Split(' ')[1].Trim().Split('/')[2].Trim();

                if (!HelpExists(helpReferenceNumber)) {

                    RunDatabaseQuery("INSERT INTO help (SenderNumber, Code, DateOfIncident, ReferenceNumber, DateTime, Status) VALUES ('" + senderNumber + "', '" + code + "', '" + dateOfIncident + "', '" + helpReferenceNumber + "', '" + GetCurrentDateTime() + "', 'PENDING')");
                    QueueOutbound("Your concern/inquiry has been received. You will be contacted by one of our representatives. Thank you.", senderNumber, referenceNumber);
                    Thread.Sleep(2000);

                    DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'HelpSoundPath'");

                    string helpSoundPath = configurationsTable.Rows[0]["Value"].ToString();

                    this.Invoke((MethodInvoker)delegate {
                        LoadHelpDataGridView();
                    });

                    System.Media.SoundPlayer helpNotification = new System.Media.SoundPlayer(@helpSoundPath);
                    helpNotification.Play();
                }
                else {
                    QueueOutbound("Help Error: RefNo " + helpReferenceNumber + " is already pending. Thank you for your patience.", senderNumber, referenceNumber);
                }
            }
            catch (Exception error){
                QueueOutbound("Help Error: Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
            }
        }

        private void ProcessLogin(string senderNumber, string command, string referenceNumber) {

            try {
                command = command.Replace("LOGIN", "").Trim();

                string receiverNumber = normalizeNumber(command.Split(' ')[0]);
                string usernameAttempt = command.Split(' ')[2];
                string passwordAttempt = command.Split(' ')[3];
                string productCode = command.Split(' ')[1];
                
                if (CheckIfUsernameExists(usernameAttempt)) {

                    DataTable userDetails = RunQueryDataTable("SELECT * FROM users WHERE Username = '" + usernameAttempt + "'");
                    string password = userDetails.Rows[0]["Password"].ToString();
                    string username = userDetails.Rows[0]["Username"].ToString();
                    string phoneNumber = userDetails.Rows[0]["PhoneNumber"].ToString();
                    int loginAttempts = int.Parse(userDetails.Rows[0]["LoginAttempts"].ToString());

                    if(loginAttempts < 5) {

                        if (password == passwordAttempt) {
                            InsertProxyMessage(phoneNumber, receiverNumber + " " + productCode, "LOAD");
                        }
                        else {
                            QueueOutbound("Login Error: Invalid Password. You have " + (5 - (loginAttempts + 1)).ToString() + " remaining login attempts. (P50 Password recovery IT fee with valid ID).", senderNumber, referenceNumber);
                            UpdateUserLoginAttempts(phoneNumber, (loginAttempts + 1).ToString());
                        }
                    }
                    else {
                        QueueOutbound("Login Error: You have reached the maximum number of login attempts.", senderNumber, referenceNumber);
                    }
                }
                else {
                    QueueOutbound("Login Error: Username Does Not Exist.", senderNumber, referenceNumber);
                }
            }
            catch {
                QueueOutbound("Login Error: Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
            }
        }

        private void ProcessGSAT(string senderNumber, string command, string referenceNumber) {
            
            try {
                string receiverNumber = command.Split(' ')[1].Trim();
                string codeDescription = command.Split(' ')[2].Trim();
                
                double price = double.Parse(Regex.Replace(codeDescription, "[A-Za-z ]", "").ToString());
                double income = GetPercentOfIncomeGSAT();

                if (GSATAvailable()) {

                    if (GSATCodeUsable(codeDescription)) {

                        if (HasLoadCreditBalance(senderNumber, (price * (1 - income)).ToString())){

                            DataTable GSATCodesTable = RunQueryDataTable("SELECT * FROM code_gsat WHERE CodeDescription = '" + codeDescription + "' AND Status = 'UNUSED'");
                            string productPin = GSATCodesTable.Rows[0]["ProductPin"].ToString();

                            RunDatabaseQuery("UPDATE code_gsat SET Status = 'USED' WHERE ProductPin = '" + productPin + "'");
                            DeductLoadCredit(senderNumber, (price * (1 - income)).ToString());
                            RunDatabaseQuery("INSERT INTO history_income (PhoneNumber, Income, Type, DateTime, ReferenceNumber) VALUES ('" + senderNumber + "', '" + (income * price).ToString() + "', 'GSAT', '" + GetCurrentDateTime() + "', '" + referenceNumber + "')");
                            RunDatabaseQuery("INSERT INTO history_gsat (SenderNumber, ProductPin, ReceiverNumber, DateTime, ReferenceNumber) VALUES ('" + senderNumber + "', '" + productPin + "', '" + receiverNumber + "', '" + GetCurrentDateTime() + "', '" + referenceNumber + "')");

                            QueueOutbound("Product: " + codeDescription + ". Pin: " + productPin + ". RefNo: " + referenceNumber + Environment.NewLine + GetCurrentDateTime(), receiverNumber, referenceNumber);
                            Thread.Sleep(2000);
                            QueueOutbound("(1/2) To load, key-in GPINOY <space> <ACCESS CARD NUMBER> <space> <PIN NUMBER> and send to a GPINOY Gateway.", receiverNumber, referenceNumber);
                            Thread.Sleep(2000);
                            QueueOutbound("(2/2) Ex.: GPINOY 1234567891000000 1234567891111111 Send to: 09088816061 09498890768 09498890769 09985897968 09173152381 09178540321.", receiverNumber, referenceNumber);
                            Thread.Sleep(2000);
                            QueueOutbound("(1/2) " + codeDescription + "(" + (price * (1 - income)).ToString() + ") has been loaded to " + receiverNumber + ". " + "RefNo: " + referenceNumber + Environment.NewLine + GetCurrentDateTime(), senderNumber, referenceNumber);
                            Thread.Sleep(2000);
                            QueueOutbound("(2/2) new load wallet balance: P" + GetUserBalance(senderNumber), senderNumber, referenceNumber);
                            Thread.Sleep(2000);
                        }
                        else {
                            QueueOutbound("GSAT Error: You have insufficient balance to complete the transaction.", senderNumber, referenceNumber);
                        }
                    }
                    else {
                        QueueOutbound("GSAT Error: Invalid Product Code. Please use the correct product code.", senderNumber, referenceNumber);
                    }
                }
                else {
                    QueueOutbound("GSAT Error: GSAT loading is currently unavailable. Please try again later.", senderNumber, referenceNumber);
                }
            }
            catch (Exception error){
                QueueOutbound("GSAT Error: Invalid command. Pls make sure your format is correct and your message does not exceed 160 characters.", senderNumber, referenceNumber);
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

                Thread.Sleep(2000);
            }
        }
        #endregion

        #region THREAD - Process Smart Loading Queue
        private void ProcessSmartLoadingQueue() {
            while (true) {

                string query = "SELECT * FROM loads WHERE Status = 'PENDING' AND (Carrier = 'SMART' OR Carrier = 'TNT' OR Carrier = 'PLDT' OR Carrier = 'CIGNAL')";

                DataTable loadQueueTable = RunQueryDataTable(query);

                foreach (DataRow row in loadQueueTable.Rows) {

                    SmartRetailerIsSending = true;

                    string productCodeQuery = "SELECT * FROM product_codes WHERE ProductCode = '" + row["ProductCode"].ToString() + "' AND Carrier = '" + row["Carrier"].ToString() + "'";

                    DataTable productCodeTable = RunQueryDataTable(productCodeQuery);

                    string senderNumber = row["SenderNumber"].ToString();
                    string receiverNumber = row["ReceiverNumber"].ToString();
                    string carrier = row["Carrier"].ToString();
                    string referenceNumber = row["ReferenceNumber"].ToString();
                    string price = productCodeTable.Rows[0]["Price"].ToString();

                    if (carrier == "SMART") {

                        string keyword = productCodeTable.Rows[0]["KeywordUSSD"].ToString();

                        SendMessageViaSmart(keyword + " " + receiverNumber, "343");

                        RunDatabaseQuery("UPDATE loads SET Status = 'PROCESSING' WHERE ReferenceNumber = '" + referenceNumber + "'");

                        Thread.Sleep(2000);
                    }

                    if (carrier == "TNT") {

                        string keyword = productCodeTable.Rows[0]["KeywordUSSD"].ToString();

                        bool isRegularLoad = keyword.Contains("Load");

                        if (isRegularLoad) {

                            SendMessageViaSmart(keyword + " " + receiverNumber, "343");
                        }
                        else {

                            SendMessageViaSmart(keyword + " " + receiverNumber, "4540");
                        }

                        RunDatabaseQuery("UPDATE loads SET Status = 'PROCESSING' WHERE ReferenceNumber = '" + referenceNumber + "'");

                        Thread.Sleep(2000);
                    }

                    if (carrier == "PLDT") {

                        string keyword = productCodeTable.Rows[0]["KeywordUSSD"].ToString();

                        SendMessageViaSmart(keyword + " " + receiverNumber, "4122");

                        RunDatabaseQuery("UPDATE loads SET Status = 'PROCESSING' WHERE ReferenceNumber = '" + referenceNumber + "'");

                        Thread.Sleep(2000);
                    }

                    if (carrier == "CIGNAL") {

                        string keyword = productCodeTable.Rows[0]["KeywordUSSD"].ToString();

                        SendMessageViaSmart(keyword + " " + receiverNumber, "3443");

                        RunDatabaseQuery("UPDATE loads SET Status = 'PROCESSING' WHERE ReferenceNumber = '" + referenceNumber + "'");

                        Thread.Sleep(2000);
                    }
                }

                SmartRetailerIsSending = false;

                ReceiveMessageRetailerSmart();
            }
        }

        private void ReceiveMessageRetailerSmart() {
            string[] splitDisplacerNewLine = new string[] { Environment.NewLine };
            string[] splitDisplacerCMT = new string[] { "+CMT" };
            string[] splitDisplacerTo = new string[] { "to" };

            string messageReceived;

            try {

                using (SerialPort smartPort = new SerialPort(SmartRetailerPort, 115200)) {
                    smartPort.Open();
                    smartPort.NewLine = Environment.NewLine;
                    smartPort.Write("AT+CNMI=1,2,0,0,0\r");
                    Thread.Sleep(100);
                    smartPort.Write("AT\r\n");
                    Thread.Sleep(100);
                    smartPort.Write("AT+CMGF=1\r\n");
                    Thread.Sleep(100);

                    smartPort.Write("AT+CMGL=\"ALL\"\r\n");
                    Thread.Sleep(100);

                    string existing = smartPort.ReadExisting();

                    if (existing.Contains("+CMT")) {

                        this.Invoke((MethodInvoker)delegate {
                            homeTextBoxSmart.Text = homeTextBoxSmart.Text + existing;
                            homeTextBoxSmart.SelectionStart = homeTextBoxSmart.Text.Length;
                            homeTextBoxSmart.ScrollToCaret();
                        });

                        messageReceived = existing.Split(splitDisplacerCMT, StringSplitOptions.None)[1];
                        messageReceived = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0] + Environment.NewLine + messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1];

                        string messageContent = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1].Trim();
                        string senderNumber = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0].Split('"')[1].Trim();

                        RunDatabaseQuery("INSERT INTO messages_smart (Sender, Message, DateTime) VALUES ('" + senderNumber + "', '" + messageContent + "', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')");

                        if (messageContent.Contains("loaded")) {
                            string receiverNumber = normalizeNumber(messageContent.Split(splitDisplacerTo, StringSplitOptions.None)[1].Trim().Split('.')[0].Trim());
                            string amount = messageContent.Split('(')[0].Trim().Split(' ').Last().Trim().Split('.')[0].Replace("P", "");

                            DataTable loadsTable = RunQueryDataTable("SELECT * FROM loads WHERE Status = 'PROCESSING' AND ReceiverNumber = '" + receiverNumber + "'");

                            string referenceNumber = loadsTable.Rows[0]["ReferenceNumber"].ToString();
                            string loaderNumber = loadsTable.Rows[0]["SenderNumber"].ToString();
                            string telecomResponse = messageContent;

                            RunDatabaseQuery("DELETE FROM loads WHERE ReceiverNumber = '" + receiverNumber + "' AND ReferenceNumber = '" + referenceNumber + "'");
                            RunDatabaseQuery("UPDATE history_load SET TelecomResponse = '" + telecomResponse + "' WHERE ReferenceNumber = '" + referenceNumber + "'");
                        }
                    }
                }
            }
            catch (Exception error) {
            }
        }
        #endregion

        #region THREAD - Process Globe Loading Queue
        private void ProcessGlobeLoadingQueue() {

            while (true) {
                string query = "SELECT * FROM loads WHERE Status = 'PENDING' AND (Carrier = 'GLOBE' OR Carrier = 'TM')";

                DataTable loadQueueTable = RunQueryDataTable(query);

                foreach (DataRow loadQueueRow in loadQueueTable.Rows) {

                    string productCodeQuery = "SELECT * FROM product_codes WHERE ProductCode = '" + loadQueueRow["ProductCode"].ToString() + "' AND Carrier = '" + loadQueueRow["Carrier"].ToString() + "'";

                    DataTable productCodeTable = RunQueryDataTable(productCodeQuery);

                    string senderNumber = loadQueueRow["SenderNumber"].ToString();
                    string receiverNumber = loadQueueRow["ReceiverNumber"].ToString();
                    string carrier = loadQueueRow["Carrier"].ToString();
                    string referenceNumber = loadQueueRow["ReferenceNumber"].ToString();
                    string price = productCodeTable.Rows[0]["Price"].ToString();
                    string USSDPattern = productCodeTable.Rows[0]["KeywordUSSD"].ToString();

                    if (!GlobeOnePortDialingUSSD) {

                        GlobeOnePortDialingUSSD = true;
                        Thread dialGlobePortOneUSSDThread = new Thread(() => GlobeOneDialUSSD(USSDPattern, receiverNumber));
                        dialGlobePortOneUSSDThread.Start();
                        RunDatabaseQuery("UPDATE loads SET Status = 'PROCESSING' WHERE ReferenceNumber = '" + referenceNumber + "'");
                    }
                    else if (!GlobeTwoPortDialingUSSD) {

                        GlobeTwoPortDialingUSSD = true;
                        Thread dialGlobePortTwoUSSDThread = new Thread(() => GlobeTwoDialUSSD(USSDPattern, receiverNumber));
                        dialGlobePortTwoUSSDThread.Start();
                        RunDatabaseQuery("UPDATE loads SET Status = 'PROCESSING' WHERE ReferenceNumber = '" + referenceNumber + "'");
                    }
                    else if (!GlobeThreePortDialingUSSD) {

                        GlobeThreePortDialingUSSD = true;
                        Thread dialGlobePortThreeUSSDThread = new Thread(() => GlobeThreeDialUSSD(USSDPattern, receiverNumber));
                        dialGlobePortThreeUSSDThread.Start();
                        RunDatabaseQuery("UPDATE loads SET Status = 'PROCESSING' WHERE ReferenceNumber = '" + referenceNumber + "'");
                    }
                }

                if (!GlobeOnePortDialingUSSD) {
                    ReceiveMessageRetailerGlobeOne();
                }

                if (!GlobeTwoPortDialingUSSD) {
                    ReceiveMessageRetailerGlobeTwo();
                }

                if (!GlobeThreePortDialingUSSD) {
                    ReceiveMessageRetailerGlobeThree();
                }
            }
        }

        private void GlobeOneDialUSSD(string USSDPattern, string phoneNumber) {

            DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'GlobeUSSD'");

            string globeUSSDNumber = configurationsTable.Rows[0]["Value"].ToString();

            string[] USSDReply = USSDPattern.Replace("n", phoneNumber).Split('/');
            int USSDCounter = 0;

            bool stillProcessing = true;

            try {
                using (SerialPort globePort = new SerialPort(GlobeRetailerOnePort, 115200)) {

                    globePort.Open();
                    Thread.Sleep(200);
                    globePort.NewLine = Environment.NewLine;
                    globePort.Write("AT\r\n");
                    Thread.Sleep(200);
                    globePort.Write("AT\r\n");
                    Thread.Sleep(200);
                    globePort.Write("AT+CMGF=1\r\n");
                    Thread.Sleep(200);
                    globePort.Write("AT+CUSD=1,\"" + globeUSSDNumber + "\", 15\r\n");
                    Thread.Sleep(200);

                    while (stillProcessing) {
                        string existing = globePort.ReadExisting();

                        this.Invoke((MethodInvoker)delegate {
                            homeTextBoxGlobe.Text = homeTextBoxGlobe.Text + existing;
                            homeTextBoxGlobe.SelectionStart = homeTextBoxGlobe.Text.Length;
                            homeTextBoxGlobe.ScrollToCaret();
                        });

                        if (existing.Contains("+CUSD:")) {
                            if (existing.Contains("Enter 11")) {
                                USSDCounter = 1;
                            }
                            existing = "";
                            globePort.Write("AT+CUSD=1,\"" + USSDReply[USSDCounter] + "\", 15\r\n");
                            USSDCounter++;
                        }

                        if (USSDCounter == USSDReply.Length) {
                            USSDCounter = 0;
                            stillProcessing = false;
                        }
                    }
                    globePort.Close();
                }
            }
            catch (Exception error) {
            }

            GlobeOnePortDialingUSSD = false;
        }

        private void GlobeTwoDialUSSD(string USSDPattern, string phoneNumber) {

            DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'GlobeUSSD'");

            string globeUSSDNumber = configurationsTable.Rows[0]["Value"].ToString();

            string[] USSDReply = USSDPattern.Replace("n", phoneNumber).Split('/');
            int USSDCounter = 0;

            bool stillProcessing = true;

            try {
                using (SerialPort globeTwoPort = new SerialPort(GlobeRetailerTwoPort, 115200)) {

                    globeTwoPort.Open();
                    Thread.Sleep(200);
                    globeTwoPort.NewLine = Environment.NewLine;
                    globeTwoPort.Write("AT\r\n");
                    Thread.Sleep(200);
                    globeTwoPort.Write("AT\r\n");
                    Thread.Sleep(200);
                    globeTwoPort.Write("AT+CMGF=1\r\n");
                    Thread.Sleep(200);
                    globeTwoPort.Write("AT+CUSD=1,\"" + globeUSSDNumber + "\", 15\r\n");
                    Thread.Sleep(200);

                    while (stillProcessing) {
                        string existing = globeTwoPort.ReadExisting();

                        this.Invoke((MethodInvoker)delegate {
                            homeTextBoxGlobe.Text = homeTextBoxGlobe.Text + existing;
                            homeTextBoxGlobe.SelectionStart = homeTextBoxGlobe.Text.Length;
                            homeTextBoxGlobe.ScrollToCaret();
                        });

                        if (existing.Contains("+CUSD:")) {
                            if (existing.Contains("Enter 11")) {
                                USSDCounter = 1;
                            }
                            existing = "";
                            globeTwoPort.Write("AT+CUSD=1,\"" + USSDReply[USSDCounter] + "\", 15\r\n");
                            USSDCounter++;
                        }

                        if (USSDCounter == USSDReply.Length) {
                            USSDCounter = 0;
                            stillProcessing = false;
                        }
                    }
                    globeTwoPort.Close();
                }
            }
            catch (Exception error) {
            }

            GlobeTwoPortDialingUSSD = false;
        }

        private void GlobeThreeDialUSSD(string USSDPattern, string phoneNumber) {

            DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = 'GlobeUSSD'");

            string globeUSSDNumber = configurationsTable.Rows[0]["Value"].ToString();

            string[] USSDReply = USSDPattern.Replace("n", phoneNumber).Split('/');
            int USSDCounter = 0;

            bool stillProcessing = true;

            try {
                using (SerialPort globeThreePort = new SerialPort(GlobeRetailerThreePort, 115200)) {

                    globeThreePort.Open();
                    Thread.Sleep(200);
                    globeThreePort.NewLine = Environment.NewLine;
                    globeThreePort.Write("AT\r\n");
                    Thread.Sleep(200);
                    globeThreePort.Write("AT\r\n");
                    Thread.Sleep(200);
                    globeThreePort.Write("AT+CMGF=1\r\n");
                    Thread.Sleep(200);
                    globeThreePort.Write("AT+CUSD=1,\"" + globeUSSDNumber + "\", 15\r\n");
                    Thread.Sleep(200);

                    while (stillProcessing) {
                        string existing = globeThreePort.ReadExisting();

                        this.Invoke((MethodInvoker)delegate {
                            homeTextBoxGlobe.Text = homeTextBoxGlobe.Text + existing;
                            homeTextBoxGlobe.SelectionStart = homeTextBoxGlobe.Text.Length;
                            homeTextBoxGlobe.ScrollToCaret();
                        });

                        if (existing.Contains("+CUSD:")) {
                            if (existing.Contains("Enter 11")) {
                                USSDCounter = 1;
                            }
                            existing = "";
                            globeThreePort.Write("AT+CUSD=1,\"" + USSDReply[USSDCounter] + "\", 15\r\n");
                            USSDCounter++;
                        }

                        if (USSDCounter == USSDReply.Length) {
                            USSDCounter = 0;
                            stillProcessing = false;
                        }
                    }
                    globeThreePort.Close();
                }
            }
            catch (Exception error) {
            }

            GlobeOnePortDialingUSSD = false;
        }

        private void ReceiveMessageRetailerGlobeOne() {
            string[] splitDisplacerNewLine = new string[] { Environment.NewLine };
            string[] splitDisplacerCMT = new string[] { "+CMT" };
            string[] splitDisplacerLoaded = new string[] { "loaded" };
            string[] splitDisplacerTo = new string[] { "to" };

            string messageReceived;

            try {

                using (SerialPort globeOnePort = new SerialPort(GlobeRetailerOnePort, 115200)) {
                    globeOnePort.Open();
                    globeOnePort.NewLine = Environment.NewLine;
                    globeOnePort.Write("AT+CNMI=1,2,0,0,0\r");
                    Thread.Sleep(100);
                    globeOnePort.Write("AT\r\n");
                    Thread.Sleep(100);
                    globeOnePort.Write("AT+CMGF=1\r\n");
                    Thread.Sleep(100);

                    globeOnePort.Write("AT+CMGL=\"ALL\"\r\n");
                    Thread.Sleep(100);

                    string existing = globeOnePort.ReadExisting();

                    if (existing.Contains("+CMT")) {

                        this.Invoke((MethodInvoker)delegate {
                            homeTextBoxGlobe.Text = homeTextBoxGlobe.Text + existing;
                            homeTextBoxGlobe.SelectionStart = homeTextBoxGlobe.Text.Length;
                            homeTextBoxGlobe.ScrollToCaret();
                        });

                        messageReceived = existing.Split(splitDisplacerCMT, StringSplitOptions.None)[1];
                        messageReceived = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0] + Environment.NewLine + messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1];

                        string messageContent = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1].Trim();
                        string senderNumber = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0].Split('"')[1].Trim();

                        RunDatabaseQuery("INSERT INTO messages_globe (Sender, Message, DateTime) VALUES ('" + senderNumber + "', '" + messageContent + "', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')");

                        if (messageContent.Contains("loaded")) {
                            string receiverNumber = normalizeNumber(messageContent.Split(splitDisplacerTo, StringSplitOptions.None)[1].Trim().Split('.')[0]);
                            string amount = Regex.Replace(messageContent.Split(splitDisplacerLoaded, StringSplitOptions.None)[1].Trim().Split(' ')[0].Trim(), "[A-Za-z ]", "");

                            DataTable loadsTable = RunQueryDataTable("SELECT * FROM loads WHERE Status = 'PROCESSING' AND ReceiverNumber = '" + receiverNumber + "'");

                            string referenceNumber = loadsTable.Rows[0]["ReferenceNumber"].ToString();
                            string loaderNumber = loadsTable.Rows[0]["SenderNumber"].ToString();
                            string telecomResponse = messageContent;

                            RunDatabaseQuery("DELETE FROM loads WHERE ReceiverNumber = '" + receiverNumber + "' AND ReferenceNumber = '" + referenceNumber + "'");
                            RunDatabaseQuery("UPDATE history_load SET TelecomResponse = '" + telecomResponse + "' WHERE ReferenceNumber = '" + referenceNumber + "'");
                        }
                    }
                }
            }
            catch (Exception error) {
            }
        }

        private void ReceiveMessageRetailerGlobeTwo() {
            string[] splitDisplacerNewLine = new string[] { Environment.NewLine };
            string[] splitDisplacerCMT = new string[] { "+CMT" };
            string[] splitDisplacerLoaded = new string[] { "loaded" };
            string[] splitDisplacerTo = new string[] { "to" };

            string messageReceived;

            try {

                using (SerialPort globeTwoPort = new SerialPort(GlobeRetailerTwoPort, 115200)) {
                    globeTwoPort.Open();
                    globeTwoPort.NewLine = Environment.NewLine;
                    globeTwoPort.Write("AT+CNMI=1,2,0,0,0\r");
                    Thread.Sleep(100);
                    globeTwoPort.Write("AT\r\n");
                    Thread.Sleep(100);
                    globeTwoPort.Write("AT+CMGF=1\r\n");
                    Thread.Sleep(100);

                    globeTwoPort.Write("AT+CMGL=\"ALL\"\r\n");
                    Thread.Sleep(100);

                    string existing = globeTwoPort.ReadExisting();

                    if (existing.Contains("+CMT")) {

                        this.Invoke((MethodInvoker)delegate {
                            homeTextBoxGlobe.Text = homeTextBoxGlobe.Text + existing;
                            homeTextBoxGlobe.SelectionStart = homeTextBoxGlobe.Text.Length;
                            homeTextBoxGlobe.ScrollToCaret();
                        });

                        messageReceived = existing.Split(splitDisplacerCMT, StringSplitOptions.None)[1];
                        messageReceived = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0] + Environment.NewLine + messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1];

                        string messageContent = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1].Trim();
                        string senderNumber = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0].Split('"')[1].Trim();

                        RunDatabaseQuery("INSERT INTO messages_globe (Sender, Message, DateTime) VALUES ('" + senderNumber + "', '" + messageContent + "', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')");

                        if (messageContent.Contains("loaded")) {
                            string receiverNumber = normalizeNumber(messageContent.Split(splitDisplacerTo, StringSplitOptions.None)[1].Trim().Split('.')[0]);
                            string amount = Regex.Replace(messageContent.Split(splitDisplacerLoaded, StringSplitOptions.None)[1].Trim().Split(' ')[0].Trim(), "[A-Za-z ]", "");

                            DataTable loadsTable = RunQueryDataTable("SELECT * FROM loads WHERE Status = 'PROCESSING' AND ReceiverNumber = '" + receiverNumber + "'");

                            string referenceNumber = loadsTable.Rows[0]["ReferenceNumber"].ToString();
                            string loaderNumber = loadsTable.Rows[0]["SenderNumber"].ToString();
                            string telecomResponse = messageContent;

                            RunDatabaseQuery("DELETE FROM loads WHERE ReceiverNumber = '" + receiverNumber + "' AND ReferenceNumber = '" + referenceNumber + "'");
                            RunDatabaseQuery("UPDATE history_load SET TelecomResponse = '" + telecomResponse + "' WHERE ReferenceNumber = '" + referenceNumber + "'");
                        }
                    }
                }
            }
            catch (Exception error) {
            }
        }

        private void ReceiveMessageRetailerGlobeThree() {
            string[] splitDisplacerNewLine = new string[] { Environment.NewLine };
            string[] splitDisplacerCMT = new string[] { "+CMT" };
            string[] splitDisplacerLoaded = new string[] { "loaded" };
            string[] splitDisplacerTo = new string[] { "to" };

            string messageReceived;

            try {

                using (SerialPort globeTwoPort = new SerialPort(GlobeRetailerTwoPort, 115200)) {
                    globeTwoPort.Open();
                    globeTwoPort.NewLine = Environment.NewLine;
                    globeTwoPort.Write("AT+CNMI=1,2,0,0,0\r");
                    Thread.Sleep(100);
                    globeTwoPort.Write("AT\r\n");
                    Thread.Sleep(100);
                    globeTwoPort.Write("AT+CMGF=1\r\n");
                    Thread.Sleep(100);

                    globeTwoPort.Write("AT+CMGL=\"ALL\"\r\n");
                    Thread.Sleep(100);

                    string existing = globeTwoPort.ReadExisting();

                    if (existing.Contains("+CMT")) {

                        this.Invoke((MethodInvoker)delegate {
                            homeTextBoxGlobe.Text = homeTextBoxGlobe.Text + existing;
                            homeTextBoxGlobe.SelectionStart = homeTextBoxGlobe.Text.Length;
                            homeTextBoxGlobe.ScrollToCaret();
                        });

                        messageReceived = existing.Split(splitDisplacerCMT, StringSplitOptions.None)[1];
                        messageReceived = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0] + Environment.NewLine + messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1];

                        string messageContent = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[1].Trim();
                        string senderNumber = messageReceived.Split(splitDisplacerNewLine, StringSplitOptions.None)[0].Split('"')[1].Trim();

                        RunDatabaseQuery("INSERT INTO messages_globe (Sender, Message, DateTime) VALUES ('" + senderNumber + "', '" + messageContent + "', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')");

                        if (messageContent.Contains("loaded")) {
                            string receiverNumber = normalizeNumber(messageContent.Split(splitDisplacerTo, StringSplitOptions.None)[1].Trim().Split('.')[0]);
                            string amount = Regex.Replace(messageContent.Split(splitDisplacerLoaded, StringSplitOptions.None)[1].Trim().Split(' ')[0].Trim(), "[A-Za-z ]", "");

                            DataTable loadsTable = RunQueryDataTable("SELECT * FROM loads WHERE Status = 'PROCESSING' AND ReceiverNumber = '" + receiverNumber + "'");

                            string referenceNumber = loadsTable.Rows[0]["ReferenceNumber"].ToString();
                            string loaderNumber = loadsTable.Rows[0]["SenderNumber"].ToString();
                            string telecomResponse = messageContent;

                            RunDatabaseQuery("DELETE FROM loads WHERE ReceiverNumber = '" + receiverNumber + "' AND ReferenceNumber = '" + referenceNumber + "'");
                            RunDatabaseQuery("UPDATE history_load SET TelecomResponse = '" + telecomResponse + "' WHERE ReferenceNumber = '" + referenceNumber + "'");
                        }
                    }
                }
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }
        }
        #endregion

        #region METHODS - User Manipulation
        private void RegisterUser(string username, string password, string phoneNumber, string fullName, string birthDate, string address, string role, string activatedBy) {

            int pins = int.Parse(GetNumberOfRegistrationPins(role));
            string query;

            query = "INSERT INTO users VALUES (null, '" + username + "', '" + password + "', '" + phoneNumber + "', '" + fullName + "', '" + birthDate + "', '" + address + "', '" + role + "', '" + activatedBy + "', 0, 0, '" + pins + "', '" + GetCurrentDateTime() + "')";

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

        private void SendMessageViaSmart(string message, string recepient) {
            try {
                using (SerialPort smsPort = new SerialPort(SmartRetailerPort, 115200)) {

                    smsPort.Open();
                    Thread.Sleep(200);
                    smsPort.NewLine = Environment.NewLine;
                    smsPort.Write("AT\r\n");
                    Thread.Sleep(200);
                    smsPort.Write("AT+CMGF=1\r");
                    Thread.Sleep(200);
                    smsPort.Write("AT+CMGS=\"" + recepient + "\"\r\n");
                    Thread.Sleep(200);
                    smsPort.Write(message + "\x1A");
                    Thread.Sleep(200);

                    string existing = smsPort.ReadExisting();

                    this.Invoke((MethodInvoker)delegate {
                        homeTextBoxSmart.Text = homeTextBoxSmart.Text + existing;
                        homeTextBoxSmart.SelectionStart = homeTextBoxSmart.Text.Length;
                        homeTextBoxSmart.ScrollToCaret();
                    });
                    if (existing.Contains("AT+CMGS")) {

                    }

                    smsPort.Close();
                }
            }
            catch (Exception error) {
                MessageBox.Show(error.Message);
            }
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

            string query = "SELECT * FROM users WHERE PhoneNumber = '" + number + "'";

            DataTable userTable = RunQueryDataTable(query);

            if (userTable.Rows.Count > 0) {
                userExists = true;
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

            bool validDate = false;

            try {
                string month = date.Split('-')[0];
                string day = date.Split('-')[1];
                string year = date.Split('-')[2];

                validDate = (((int.Parse(month) <= 12) && (int.Parse(month) > 0)) && (year.Length == 4)) && ((int.Parse(day) > 0) && (int.Parse(day) <= 31));
            }
            catch {
                validDate = false;
            }


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
            
            string query = "SELECT RoleRank FROM roles WHERE Role = '" + role + "'";
            int rank = 0;

            DataTable dataTableRole = RunQueryDataTable(query);

            if(dataTableRole.Rows.Count > 0) {
                rank = int.Parse(dataTableRole.Rows[0]["RoleRank"].ToString());
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
                if (senderRank == 1) {
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

        private bool HasLoadCreditBalance(string phoneNumber, string amount) {

            bool hasLoadCreditBalance = false;

            double balance = 0;

            string query = "SELECT Balance FROM users WHERE PhoneNumber = '" + phoneNumber + "'";

            DataTable dataTableBalance = RunQueryDataTable(query);

            if (dataTableBalance.Rows.Count > 0) {
                balance = double.Parse(dataTableBalance.Rows[0]["Balance"].ToString());
            }

            hasLoadCreditBalance = double.Parse(amount) <= balance;

            return hasLoadCreditBalance;
        }

        private bool HasPinBalance(string phoneNumber, int amount) {

            bool hasPinBalance = false;

            int balance = 0;

            string query = "SELECT Pins FROM users WHERE PhoneNumber = '" + phoneNumber + "'";

            DataTable userTable = RunQueryDataTable(query);

            if (userTable.Rows.Count > 0) {
                balance = int.Parse(userTable.Rows[0]["Pins"].ToString());
            }

            hasPinBalance = (balance >= amount);

            return hasPinBalance;
        }

        private double GetPercentOfIncomeTLC(string senderNumber, string receiverNumber) {

            double percentage = 0;

            string senderRole = GetUserRole(senderNumber);
            string receiverRole = GetUserRole(receiverNumber);

            int senderRank = GetRoleRank(senderRole);
            int receiverRank = GetRoleRank(receiverRole);

            string query;

            if (senderRank == receiverRank) {
                percentage = 0;
            }
            else {
                DataTable dataTablePercentage;

                for (int i = 0; i < (senderRank - receiverRank); i++) {

                    query = "SELECT TLCIncomePercentage FROM roles WHERE RoleRank = '" + (i + receiverRank).ToString() + "'";

                    dataTablePercentage = RunQueryDataTable(query);

                    percentage = percentage + double.Parse(dataTablePercentage.Rows[0]["TLCIncomePercentage"].ToString());
                }
            }

            return percentage;
        }

        private double GetPercentOfIncomeGSAT() {
            string parameter = "GPinoyLoadPercentage";

            DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = '" + parameter + "'");

            return double.Parse(configurationsTable.Rows[0]["Value"].ToString());
        }

        private bool HasSameTransactionWithin30Minutes(string senderNumber, string receiverNumber, string amount, string type) {

            bool hasSameTransactionWithin30Minutes = false;

            double repeatTransactionMinutes;

            string query = "SELECT Value FROM configurations WHERE Parameter = 'RepeatTransactionMinutes'";
            DataTable configurationsTable = RunQueryDataTable(query);

            repeatTransactionMinutes = double.Parse(configurationsTable.Rows[0]["Value"].ToString());

            if (type == "TLC") {
                query = "SELECT DateTime FROM history_tlc WHERE SenderNumber = '" + senderNumber + "' AND ReceiverNumber = '" + receiverNumber + "' AND Amount LIKE '%" + amount + "%' AND DateTime >= NOW() - INTERVAL " + repeatTransactionMinutes + " MINUTE";
            }
            else if (type == "TTU") {
                query = "SELECT DateTime FROM history_ttu WHERE SenderNumber = '" + senderNumber + "' AND ReceiverNumber = '" + receiverNumber + "' AND Amount = '" + amount + "' AND DateTime >= NOW() - INTERVAL " + repeatTransactionMinutes + " MINUTE";
            }
            else if (type == "LOAD") {
                query = "SELECT DateTime FROM history_load WHERE SenderNumber = '" + senderNumber + "' AND ReceiverNumber = '" + receiverNumber + "' AND Amount LIKE '%" + amount + "%' AND DateTime >= NOW() - INTERVAL " + repeatTransactionMinutes + " MINUTE";
            }

            DataTable tlcHistoryTable = RunQueryDataTable(query);

            if (tlcHistoryTable.Rows.Count > 0) {
                hasSameTransactionWithin30Minutes = true;
            }

            return hasSameTransactionWithin30Minutes;
        }

        private string GetUserBalance(string phoneNumber) {

            string balance;

            string query = "SELECT Balance FROM users WHERE PhoneNumber = '" + phoneNumber + "'";

            DataTable userTable = RunQueryDataTable(query);

            balance = userTable.Rows[0]["Balance"].ToString();

            return balance;
        }

        private string GetCarrier(string phoneNumber) {
            string carrier = "";

            string prefix = phoneNumber.Substring(0, 4);

            string query = "SELECT Carrier FROM carriers WHERE Number = '" + prefix + "'";

            DataTable carrierTable = RunQueryDataTable(query);

            if(carrierTable.Rows.Count > 0) {
                carrier = carrierTable.Rows[0]["Carrier"].ToString();
            }
            else {
                MessageBox.Show(phoneNumber + " is not listed in the carriers. Please Update.");
            }

            return carrier;
        }

        private bool ProductCodeExists(string productCode, string carrier) {

            bool productCodeExists = false;

            string query = "SELECT * FROM product_codes WHERE ProductCode = '" + productCode + "' AND Carrier = '" + carrier + "'";

            DataTable productCodeTable = RunQueryDataTable(query);

            if (productCodeTable.Rows.Count > 0) {

                productCodeExists = true;
            }

            return productCodeExists;
        }

        private string GetUserPassword(string phoneNumber) {

            string password;

            string query = "SELECT Password FROM users WHERE PhoneNumber = '" + phoneNumber + "'";

            DataTable userTable = RunQueryDataTable(query);

            password = userTable.Rows[0]["Password"].ToString();

            return password;
        }

        private string GetUserLoginAttempts(string phoneNumber) {

            string attempts;

            string query = "SELECT LoginAttempts FROM users WHERE PhoneNumber = '" + phoneNumber + "'";

            DataTable userTable = RunQueryDataTable(query);

            attempts = userTable.Rows[0]["LoginAttempts"].ToString();

            return attempts;
        }

        private void UpdateUserLoginAttempts(string phoneNumber, string newAttempt) {
            RunDatabaseQuery("UPDATE users SET LoginAttempts = '" + newAttempt + "' WHERE phoneNumber = '" + phoneNumber + "'");
        }

        private bool CheckIfUsernameExists(string username) {

            string query = "SELECT * FROM users WHERE Username = '" + username + "'";

            DataTable userTable = RunQueryDataTable(query);

            if(userTable.Rows.Count > 0) {
                return true;
            }
            else {
                return false;
            }
        }

        private void InsertProxyMessage(string senderNumber, string command, string commandType) {
            RunDatabaseQuery("INSERT INTO commands (SenderNumber, Command, CommandType, DateTime, Status, ReferenceNumber) VALUES ('" + senderNumber + "', '" + command + "', '" + commandType + "', '" + GetCurrentDateTime() + "', 'PENDING', '" + GenerateReferenceNumber() + "')");
        }

        private bool GSATCodeUsable(string codeDescription) {
            bool codeUsable = false;

            string query = "SELECT CodeDescription FROM code_gsat WHERE CodeDescription = '" + codeDescription + "' AND Status = 'UNUSED'";

            DataTable GSATCodesTable = RunQueryDataTable(query);

            if(GSATCodesTable.Rows.Count > 0) {
                codeUsable = true;
            }

            return codeUsable;
        }

        private bool GSATAvailable() {
            bool GSATAvailable = false;

            string query = "SELECT Value FROM configurations WHERE Parameter = 'GSATAvailable'";

            DataTable configurationsTable = RunQueryDataTable(query);

            if(configurationsTable.Rows[0]["Value"].ToString() == "YES") {
                GSATAvailable = true;
            }

            return GSATAvailable;
        }

        private bool HelpExists(string referenceNumber) {
            bool helpExists = false;

            DataTable helpTable = RunQueryDataTable("SELECT * FROM help WHERE ReferenceNumber = '" + referenceNumber + "' AND Status = 'Pending'");

            if (helpTable.Rows.Count > 0) {
                helpExists = true;
            }

            return helpExists;
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
            myReader.Read();
            this.GateWayOnePort = myReader["Value"].ToString();
            databaseConnection.Close();


            //GATEWAY ONE PORT ASSIGNING
            query = "SELECT Value FROM configurations WHERE Parameter = 'GateWayTwo'";
            commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();

            myReader = commandDatabase.ExecuteReader();
            myReader.Read();
            this.GateWayTwoPort = myReader["Value"].ToString();
            databaseConnection.Close();

            //SENDER ONE PORT ASSIGNING
            query = "SELECT Value FROM configurations WHERE Parameter = 'SenderOnePort'";
            commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();
            myReader = commandDatabase.ExecuteReader();

            myReader.Read();
            this.SenderOnePort = myReader["Value"].ToString();
            databaseConnection.Close();

            //SENDER TWO PORT ASSIGNING
            query = "SELECT Value FROM configurations WHERE Parameter = 'SenderTwoPort'";
            commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();
            myReader = commandDatabase.ExecuteReader();

            myReader.Read();
            this.SenderTwoPort = myReader["Value"].ToString();
            databaseConnection.Close();

            //GLOBE RETAILER ONE PORT ASSIGNING
            query = "SELECT Value FROM configurations WHERE Parameter = 'GlobeRetailerOnePort'";
            commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();
            myReader = commandDatabase.ExecuteReader();

            myReader.Read();
            this.GlobeRetailerOnePort = myReader["Value"].ToString();
            databaseConnection.Close();

            //GLOBE RETAILER TWO PORT ASSIGNING
            query = "SELECT Value FROM configurations WHERE Parameter = 'GlobeRetailerTwoPort'";
            commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();
            myReader = commandDatabase.ExecuteReader();

            myReader.Read();
            this.GlobeRetailerTwoPort = myReader["Value"].ToString();
            databaseConnection.Close();

            //GLOBE RETAILER THREE PORT ASSIGNING
            query = "SELECT Value FROM configurations WHERE Parameter = 'GlobeRetailerThreePort'";
            commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();
            myReader = commandDatabase.ExecuteReader();

            myReader.Read();
            this.GlobeRetailerThreePort = myReader["Value"].ToString();
            databaseConnection.Close();

            //SMART RETAILER PORT ASSIGNING
            query = "SELECT Value FROM configurations WHERE Parameter = 'SmartRetailerPort'";
            commandDatabase = new MySqlCommand(query, databaseConnection);

            databaseConnection.Open();
            myReader = commandDatabase.ExecuteReader();

            myReader.Read();
            this.SmartRetailerPort = myReader["Value"].ToString();
            databaseConnection.Close();

            //MessageBox.Show("GateWayone = " + this.GateWayOnePort);
            //MessageBox.Show("GateWaytwoPort = " + this.GateWayTwoPort);
            //MessageBox.Show("Sender one = " + this.SenderOnePort);
            //MessageBox.Show("sender two = " + this.SenderTwoPort);
            //MessageBox.Show("Globe One = " + this.GlobeRetailerOnePort);
            //MessageBox.Show("Globe Two = " + this.GlobeRetailerTwoPort);
            //MessageBox.Show("Globe Three = " + this.GlobeRetailerThreePort);
            //MessageBox.Show("Smart Port = " + this.SmartRetailerPort);
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
            //TAB - HOME
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

            //TAB - HELP
            LoadHelpDataGridView();
        }

        //HOME
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

        private double GetPercentIncomeLoad(string type) {

            string parameter = "";

            if(type == "SMART") {
                parameter = "SmartLoadPercentage";
            }
            else if (type == "GLOBE") {
                parameter = "GlobeLoadPercentage";
            }

            DataTable configurationsTable = RunQueryDataTable("SELECT Value FROM configurations WHERE Parameter = '" + parameter + "'");

            return double.Parse(configurationsTable.Rows[0]["Value"].ToString());
        }

        private string GetCurrentDateTime() {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void DeductLoadCredit(string phoneNumber, string amount) {
            RunDatabaseQuery("UPDATE users SET Balance = '" + (double.Parse(GetUserBalance(phoneNumber)) - double.Parse(amount)).ToString() + "' WHERE PhoneNumber = '" + phoneNumber + "'");
        }

        //HELP
        private void LoadHelpDataGridView() {
            HelpDataGridView.DataSource = RunQueryDataTable("SELECT * FROM help");
        }

        private void HelpDataGridView_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e) {
            int rowIndexSelected = e.RowIndex;
            string senderNumber = HelpDataGridView.Rows[rowIndexSelected].Cells["SenderNumber"].Value.ToString();
            string referenceNumber = HelpDataGridView.Rows[rowIndexSelected].Cells["ReferenceNumber"].Value.ToString();

            DataTable helpTable = RunQueryDataTable("SELECT * FROM help WHERE SenderNumber = '" + senderNumber + "' AND ReferenceNumber = '" + referenceNumber + "'");

            DataTable userTable = RunQueryDataTable("SELECT * FROM users WHERE PhoneNumber = '" + senderNumber + "'");

            helpLabelOutputSenderNumber.Text = userTable.Rows[0]["Username"].ToString();

            helpLabelOutputCode.Text = helpTable.Rows[0]["Code"].ToString();
            helpLabelOutputDateOfIncident.Text = helpTable.Rows[0]["DateOfIncident"].ToString();
            helpLabelOutputReferenceNumber.Text = helpTable.Rows[0]["ReferenceNumber"].ToString();
            helpLabelOutputDateIssued.Text = helpTable.Rows[0]["DateTime"].ToString();
            helpLabelOutputStatus.Text = helpTable.Rows[0]["Status"].ToString();
        }

        private void helpButtonSend_Click(object sender, EventArgs e) {

            if (helpTextBoxReply.Text != "") {

                string reply = helpTextBoxReply.Text;
                string senderNumber = helpLabelOutputSenderNumber.Text;
                string referenceNumber = helpLabelOutputReferenceNumber.Text;

                QueueOutbound(reply, helpLabelOutputSenderNumber.Text, "");

                RunDatabaseQuery("UPDATE help SET Status = 'MESSAGE SENT' WHERE SenderNumber = '" + senderNumber + "' AND ReferenceNumber = '" + referenceNumber + "'");

                LoadHelpDataGridView();

                MessageBox.Show("Successfully sent reply to " + senderNumber + ".");

                helpTextBoxReply.Text = "";
            }
            else {
                MessageBox.Show("Reply body can not be empty.");
            }
        }


        //SETTINGS
        private void LoadSettingsValues() {
            DataTable configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'GateWayOne'");

            settingsPortsLabelGateWayOne.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'GateWayTwo'");
            settingsPortsLabelGateWayTwo.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'SenderOnePort'");
            settingsPortsLabelSenderOne.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'SenderTwoPort'");
            settingsPortsLabelSenderTwo.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'GlobeRetailerOnePort'");
            settingsPortsLabelGlobeOne.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'GlobeRetailerTwoPort'");
            settingsPortsLabelGlobeTwo.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'GlobeRetailerThreePort'");
            settingsPortsLabelGlobeThree.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'SmartRetailerPort'");
            settingsPortsLabelSmart.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'SmartLoadPercentage'");
            settingsPortsTextBoxSmartLoading.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'GlobeLoadPercentage'");
            settingsPortsTextBoxGlobeLoading.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'GPinoyLoadPercentage'");
            settingsPortsTextBoxGSATLoading.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'HelpSoundPath'");
            TB_HelpSoundPath.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'GSATAvailable'");
            TB_GSATAvailable.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'GlobeUSSD'");
            TB_GlobeUSSD.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'RepeatTransactionMinutes'");
            TB_RepeatTransaction.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'RetailerPins'");
            TB_Retailer.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'DistributorPins'");
            TB_Distributor.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'DealerPins'");
            TB_Dealer.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'MobilePins'");
            TB_Mobile.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'CityPins'");
            TB_City.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'ProvincialPins'");
            TB_Provincial.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'MinimumTLC'");
            TB_MinimumTLC.Text = configurationsTable.Rows[0]["Value"].ToString();

            configurationsTable = RunQueryDataTable("SELECT * FROM configurations WHERE Parameter = 'MinimumTTU'");
            TB_MinimumTTU.Text = configurationsTable.Rows[0]["Value"].ToString();
        }

        private void settingsPortsButtonSaveGateWayOne_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsLabelGateWayOne.Text + "' WHERE Parameter = 'GateWayOne'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void settingsPortsButtonSaveGateWayTwo_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsLabelGateWayTwo.Text + "' WHERE Parameter = 'GateWayTwo'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void settingsPortsButtonSenderOne_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsLabelSenderOne.Text + "' WHERE Parameter = 'SenderOnePort'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void settingsPortsButtonSaveSenderTwo_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsLabelSenderTwo.Text + "' WHERE Parameter = 'SenderTwoPort'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void settingsPortsButtonSaveGlobeOne_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsLabelGlobeOne.Text + "' WHERE Parameter = 'GlobeRetailerOnePort'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void settingsPortsButtonSaveGlobeTwo_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsLabelGlobeTwo.Text + "' WHERE Parameter = 'GlobeRetailerTwoPort'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void settingsPortsButtonSaveGlobeThree_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsLabelGlobeThree.Text + "' WHERE Parameter = 'GlobeRetailerThreePort'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void settingsPortsButtonSaveSmart_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsLabelSmart.Text + "' WHERE Parameter = 'SmartRetailerPort'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void settingsPortsButtonSaveSmartLoading_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsTextBoxSmartLoading.Text + "' WHERE Parameter = 'SmartLoadPercentage'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void settingsPortsButtonSaveGlobeLoading_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsTextBoxGlobeLoading.Text + "' WHERE Parameter = 'GlobeLoadPercentage'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void settingsPortsButtonSaveGSATLoading_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + settingsPortsTextBoxGSATLoading.Text + "' WHERE Parameter = 'GPinoyLoadPercentage'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_HelpSoundPath_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_HelpSoundPath.Text + "' WHERE Parameter = 'HelpSoundPath'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_GSATAvailable_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_GSATAvailable.Text + "' WHERE Parameter = 'GSATAvailable'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_GlobeUSSD_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_GlobeUSSD.Text + "' WHERE Parameter = 'GlobeUSSD'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_RepeatTransaction_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_RepeatTransaction.Text + "' WHERE Parameter = 'RepeatTransactionMinutes'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_Retailer_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_Retailer.Text + "' WHERE Parameter = 'RetailerPins'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_Distributor_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_Distributor.Text + "' WHERE Parameter = 'DistributorPins'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_Dealer_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_Dealer.Text + "' WHERE Parameter = 'DealerPins'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_Mobile_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_Mobile.Text + "' WHERE Parameter = 'MobilePins'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_City_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_City.Text + "' WHERE Parameter = 'CityPins'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_Provincial_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_Provincial.Text + "' WHERE Parameter = 'ProvincialPins'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_MinimumTLC_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_MinimumTLC.Text + "' WHERE Parameter = 'MinimumTLC'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }

        private void B_MinimumTTU_Click(object sender, EventArgs e) {

            RunDatabaseQuery("UPDATE configurations SET Value = '" + TB_MinimumTTU.Text + "' WHERE Parameter = 'MinimumTTU'");
            MessageBox.Show("Updated");
            LoadSettingsValues();
        }
        #endregion
    }
}
