using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// Test program for Pulse Train Hat http://www.pthat.com

namespace PulseTrainHatRPMonTheFLY
{
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Private variables
        /// </summary>
        private SerialDevice serialPort = null;

        private DataWriter dataWriteObject = null;
        private DataReader dataReaderObject = null;

        private ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;

        //Pause X Axis Flag
        private int pauseXaxis = 0;

        //Conversion variables
        private Double convertRPM;
        private Double convertSTEPS;

        public MainPage()
        {
            this.InitializeComponent();

            //Set Controls
            IncreaseRPM.IsEnabled = false;
            DecreaseRPM.IsEnabled = false;
            PauseX.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
            comPortInput.IsEnabled = false;
            sendTextButton.IsEnabled = false;
            Firmware1.IsEnabled = false;
            StartX.IsEnabled = false;
            PauseX.IsEnabled = false;
            StopX.IsEnabled = false;
            GetXPulses.IsEnabled = false;
            Reset.IsEnabled = false;
            ToggleEnableLine.IsEnabled = false;
            listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();
        }

        /// <summary>
        /// ListAvailablePorts
        /// - Use SerialDevice.GetDeviceSelector to enumerate all serial devices
        /// - Attaches the DeviceInformation to the ListBox source so that DeviceIds are displayed
        /// </summary>
        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                status.Text = "Select a device and connect";

                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);
                }

                DeviceListSource.Source = listOfDevices;
                comPortInput.IsEnabled = true;
                ConnectDevices.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }

        /// <summary>
        /// comPortInput_Click: Action to take when 'Connect' button is clicked
        /// - Get the selected device index and use Id to create the SerialDevice object
        /// - Configure default settings for the serial port
        /// - Create the ReadCancellationTokenSource token
        /// - Start listening on the serial port input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void comPortInput_Click(object sender, RoutedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;

            if (selection.Count <= 0)
            {
                status.Text = "Select a device and connect";
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection[0];

            try
            {
                serialPort = await SerialDevice.FromIdAsync(entry.Id);

                // Disable the 'Connect' button
                comPortInput.IsEnabled = false;

                // Configure serial settings
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(30);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(30);

                if (LowSpeedBaud.IsChecked == true)
                {
                    serialPort.BaudRate = 115200;
                }
                else
                {
                    serialPort.BaudRate = 806400;
                }

                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;

                // Display configured settings
                status.Text = "Serial port configured successfully: ";
                status.Text += serialPort.BaudRate + "-";
                status.Text += serialPort.DataBits + "-";
                status.Text += serialPort.Parity.ToString() + "-";
                status.Text += serialPort.StopBits;

                // Set the RcvdText field to invoke the TextChanged callback
                // The callback launches an async Read task to wait for data
                rcvdText.Text = "Waiting for data...";

                // Create cancellation token object to close I/O operations when closing the device
                ReadCancellationTokenSource = new CancellationTokenSource();

                // Enable 'WRITE' button to allow sending data
                sendTextButton.IsEnabled = true;
                Firmware1.IsEnabled = true;
                StartX.IsEnabled = true;

                Reset.IsEnabled = true;
                ToggleEnableLine.IsEnabled = true;
                sendText.Text = "";
                IncreaseRPM.IsEnabled = true;
                DecreaseRPM.IsEnabled = true;

                GetXPulses.IsEnabled = true;

                Listen();
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                comPortInput.IsEnabled = true;
                sendTextButton.IsEnabled = false;
                Firmware1.IsEnabled = false;
                StartX.IsEnabled = false;
                PauseX.IsEnabled = false;
                StopX.IsEnabled = false;
                GetXPulses.IsEnabled = false;
                Reset.IsEnabled = false;
                ToggleEnableLine.IsEnabled = false;
            }
        }

        /// <summary>
        /// sendTextButton_Click: Action to take when 'WRITE' button is clicked
        /// - Create a DataWriter object with the OutputStream of the SerialDevice
        /// - Create an async task that performs the write operation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void sendTextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync();
                }
                else
                {
                    status.Text = "Select a device and connect";
                }
            }
            catch (Exception ex)
            {
                status.Text = "sendTextButton_Click: " + ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync()
        {
            Task<UInt32> storeAsyncTask;

            if (sendText.Text.Length != 0)
            {
                // Load the text from the sendText input text box to the dataWriter object
                dataWriteObject.WriteString(sendText.Text);

                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = sendText.Text + ", ";
                    status.Text += "bytes written successfully!";
                }
                //sendText.Text = "";
            }
            else
            {
                status.Text = "Enter the text you want to write and then click on 'WRITE'";
            }
        }

        /// <summary>
        /// - Create a DataReader object
        /// - Create an async task to read from the SerialDevice InputStream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Listen()
        {
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);

                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    status.Text = "Reading task was cancelled, closing device and cleaning up";
                    CloseDevice();
                }
                else
                {
                    status.Text = ex.Message;
                }
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;

            if (bytesRead > 0)
            {
                rcvdText.Text = dataReaderObject.ReadString(bytesRead);

                string input = rcvdText.Text;

                //Check if received message can be divided by 7 as our return messages are 7 bytes long
                if (input.Length % 7 == 0)
                {
                    //*********
                    for (int i = 0; i < input.Length; i += 7)
                    {
                        string sub = input.Substring(i, 7);

                        //Check if Start X command Received
                        if (sub == "RI00SX*")
                        {
                            //Enable/Disable Variables
                            StartX.IsEnabled = false;
                            StopX.IsEnabled = true;
                            PauseX.IsEnabled = true;
                            ToggleEnableLine.IsEnabled = false;
                        }

                        //Check if speed changed completed
                        if (sub == "RI00QX*")
                        {
                            //Enable RPM change buttons
                            IncreaseRPM.IsEnabled = true;
                            DecreaseRPM.IsEnabled = true;
                        }

                        //Check if Pause X Command received
                        if (sub == "RI00PX*")
                        {
                            //Replace pause X button text
                            PauseX.Content = "Resume X";

                            //Change pause X button colour
                            PauseX.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);

                            //Pause X is active
                            pauseXaxis = 1;
                        }

                        //Check if Resume X Command recieved
                        if (sub == "CI00PX*")
                        {
                            PauseX.Content = "Pause X";
                            PauseX.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                            pauseXaxis = 0;
                        }

                        //Check if Set X Axis completed
                        if (sub == "CI00CX*")
                        {
                            //Send Start X Axis Command
                            sendText.Text = "I00SX*";
                            SendDataOut();
                        }

                        //Check if X Axis Stop Recieved
                        if (sub == "RI00TX*")
                        {
                            StopX.IsEnabled = false;
                            PauseX.IsEnabled = false;
                            StartX.IsEnabled = true;

                            //Check if X Axis is Paused
                            if (pauseXaxis == 1)
                            {
                                PauseX.Content = "Pause X";
                                PauseX.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                                pauseXaxis = 0;
                            }
                        }

                        //Check if X Axis completed amount of pulses
                        if (sub == "CI00SX*")
                        {
                            StopX.IsEnabled = false;
                            PauseX.IsEnabled = false;
                            StartX.IsEnabled = true;
                        }
                    } // end of for loop
                } //endof checking length if

                status.Text = "bytes read successfully!";
            } //End of checking for bytes
        } //end of async read

        /// <summary>
        /// CancelReadTask:
        /// - Uses the ReadCancellationTokenSource to cancel read operations
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        /// <summary>
        /// CloseDevice:
        /// - Disposes SerialDevice object
        /// - Clears the enumerated device Id list
        /// </summary>
        private void CloseDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;

            comPortInput.IsEnabled = true;
            sendTextButton.IsEnabled = false;
            rcvdText.Text = "";
            listOfDevices.Clear();
        }

        /// <summary>
        /// closeDevice_Click: Action to take when 'Disconnect and Refresh List' is clicked on
        /// - Cancel all read operations
        /// - Close and dispose the SerialDevice object
        /// - Enumerate connected devices
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeDevice_Click(object sender, RoutedEventArgs e)
        {
            Disconnectserial();
        }

        private void Disconnectserial()
        {
            try
            {
                status.Text = "";
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();
                Firmware1.IsEnabled = false;
                StartX.IsEnabled = false;
                PauseX.IsEnabled = false;
                StopX.IsEnabled = false;
                GetXPulses.IsEnabled = false;
                Reset.IsEnabled = false;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }

        private async void SendDataOut()
        {
            try

            {
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync();
                }
                else
                {
                    status.Text = "Select a device and connect";
                }
            }
            catch (Exception ex)
            {
                status.Text = "Send Data: " + ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        //Formats boxes to correct string format
        private void formatboxes()
        {
            XFreq.Text = String.Format("{0:000000.000}", Convert.ToDouble(XFreq.Text));
            Xpulsecount.Text = String.Format("{0:0000000000}", Convert.ToDouble(Xpulsecount.Text));
            Xdir.Text = String.Format("{0:0}", Convert.ToDouble(Xdir.Text));
            XRampUp.Text = String.Format("{0:0}", Convert.ToDouble(XRampUp.Text));
            XRampDown.Text = String.Format("{0:0}", Convert.ToDouble(XRampDown.Text));
            Xrampdivide.Text = String.Format("{0:000}", Convert.ToDouble(Xrampdivide.Text));
            Xramppause.Text = String.Format("{0:000}", Convert.ToDouble(Xramppause.Text));
            Xadc.Text = String.Format("{0:0}", Convert.ToDouble(Xadc.Text));
            EnableX.Text = String.Format("{0:0}", Convert.ToDouble(EnableX.Text));
            FormattedX.Text = "I00CX" + XFreq.Text + Xpulsecount.Text + Xdir.Text + XRampUp.Text + XRampDown.Text + Xrampdivide.Text + Xramppause.Text + Xadc.Text + EnableX.Text + "*";
        }

        private void Firmware_Click(object sender, RoutedEventArgs e)
        {
            //Request Firmware from PTHAT
            sendText.Text = "I00FW*";
            SendDataOut();
        }

        private void GetXPulses_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Get Current X Pulses Command
            sendText.Text = "I00XP*";
            SendDataOut();
        }

        private void StartX_Click(object sender, RoutedEventArgs e)
        {
            //Convert to Frequency
            convertSTEPS = Convert.ToDouble(StepsPerRev.Text) * 0.0166666666666667;
            convertRPM = Convert.ToDouble(SetRPM.Text) * convertSTEPS;
            convertRPM = (Math.Round(convertRPM / 0.004)) * 0.004;
            XFreq.Text = Convert.ToString(convertRPM);

            //Call format method
            formatboxes();

            //Send Set X Axis
            sendText.Text = FormattedX.Text;
            SendDataOut();
        }

        private void StopX_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Stop Command
            sendText.Text = "I00TX*";
            SendDataOut();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Reset Command
            sendText.Text = "N*";
            SendDataOut();
        }

        private void PauseX_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Pause Command
            sendText.Text = "I00PX0000*";
            SendDataOut();
        }

        private void ToggleEnableLine_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Toggle Enable Line command
            sendText.Text = "I00HT*";
            SendDataOut();
        }

        private void RPM_TextChanged(object sender, TextChangedEventArgs e)
        {
            Conversions();
        }

        private void StepsPerRev_TextChanged(object sender, TextChangedEventArgs e)
        {
            Conversions();
        }

        private void Conversions()
        {
            //Check if Steps per revolution Textbox is not Null or empty
            if (!String.IsNullOrEmpty(StepsPerRev.Text.Trim()))
            {
                //Convert our Steps per revolution into a Frequency
                convertSTEPS = Convert.ToDouble(StepsPerRev.Text) * 0.0166666666666667;
            }

            //Check if RPM Textbox is not Null or empty
            if (!String.IsNullOrEmpty(RPM.Text.Trim()))
            {
                //Multiply the RPM by our new frequency
                convertRPM = Convert.ToDouble(RPM.Text) * convertSTEPS;

                //Convert our value to match the DDS Resolution
                convertRPM = (Math.Round(convertRPM / 0.004)) * 0.004;
            }

            //Format string
            HZresult.Text = Convert.ToString(convertRPM);
            HZresult.Text = String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text));
        }

        private void IncreaseRPM_Click(object sender, RoutedEventArgs e)
        {
            IncreaseRPM.IsEnabled = false;
            DecreaseRPM.IsEnabled = false;

            //Checks which checkbox is ticked and increments RPM by that value
            if (rev01.IsChecked == true)
            {
                SetRPM.Text = String.Format("{0:00000.0}", (Convert.ToDouble(SetRPM.Text) + 0.1));
            }

            if (rev1.IsChecked == true)
            {
                SetRPM.Text = String.Format("{0:00000.0}", (Convert.ToDouble(SetRPM.Text) + 1));
            }

            if (rev10.IsChecked == true)
            {
                SetRPM.Text = String.Format("{0:00000.0}", (Convert.ToDouble(SetRPM.Text) + 10));
            }

            if (rev100.IsChecked == true)
            {
                SetRPM.Text = String.Format("{0:00000.0}", (Convert.ToDouble(SetRPM.Text) + 25));
            }

            convertSTEPS = Convert.ToDouble(StepsPerRev.Text) * 0.0166666666666667;
            convertRPM = Convert.ToDouble(SetRPM.Text) * convertSTEPS;
            convertRPM = (Math.Round(convertRPM / 0.004)) * 0.004;

            FormattedXonFLY.Text = "I00QX" + String.Format("{0:000000.000}", convertRPM) + "*";
            sendText.Text = FormattedXonFLY.Text;

            SendDataOut();
        }

        private void DecreaseRPM_Click(object sender, RoutedEventArgs e)
        {
            IncreaseRPM.IsEnabled = false;
            DecreaseRPM.IsEnabled = false;

            if (rev01.IsChecked == true)
            {
                SetRPM.Text = String.Format("{0:00000.0}", (Convert.ToDouble(SetRPM.Text) - 0.1));
            }

            if (rev1.IsChecked == true)
            {
                SetRPM.Text = String.Format("{0:00000.0}", (Convert.ToDouble(SetRPM.Text) - 1));
            }

            if (rev10.IsChecked == true)
            {
                SetRPM.Text = String.Format("{0:00000.0}", (Convert.ToDouble(SetRPM.Text) - 10));
            }

            if (rev100.IsChecked == true)
            {
                SetRPM.Text = String.Format("{0:00000.0}", (Convert.ToDouble(SetRPM.Text) - 25));
            }

            convertSTEPS = Convert.ToDouble(StepsPerRev.Text) * 0.0166666666666667;
            convertRPM = Convert.ToDouble(SetRPM.Text) * convertSTEPS;
            convertRPM = (Math.Round(convertRPM / 0.004)) * 0.004;

            FormattedXonFLY.Text = "I00QX" + String.Format("{0:000000.000}", convertRPM) + "*";
            sendText.Text = FormattedXonFLY.Text;

            SendDataOut();
        }
    }
}