    using System;
    using System.IO;
    using System.Linq;
	using System.Net;
	using System.Net.Sockets;
    using System.Threading;

    /*
     * Michael Schleeweis Connor
     * Schneider Electric Fall 2018 Internship
     */
    namespace EBOImageTool
    {
        /* 
         * Command-line syntax:
         *      >ImageTool /[option] [file_location]* [ip]**
         *      	- Note: (*) denotes dependent console parameters,
							(**) is dependent on both
		 *
         * Command-line options:                Output:
         *      /? - Help                       Char_String
         *      /f - Min_FB_Version             Char_String
         *      /h - Header (entire)            Char_String
         *      /n - Model_Name                 Char_String
         *      /s - Min_Script_Version         Char_String
         *      /t - Type                       Boolean
         *      /w - Reformat to Wireshark*     ConversionFile.pcapng
         *           - Note: The above option will place the resulting 
         *                   file where the command was executed, and 
         *                   will overwrite any previous conversion.
		 *		/r - Replay Instructions**		NPDUs -> MPX
         */
        class ImageTool
        {
            /*
             * Main/driver method. Handles user requests, sending updates to the
             * console.
             * 
             * input:
             * string[] Args: Command-line arguments. At least Args[0] required
             *                Args[0]: Option
             *                Args[1]: EBO Image File location
             *                Args[2]: MPX IP address
             */
            private static void Main(string[] Args)
            {
                string ErrorMessage = "Parameter Syntax:\r\n" +
                                      "  >ImageTool /[option] [file_location] [mpxip]\r\n" +
                                      "     **[file_location] is relative to\r\n" +
                                      "       the current working directory.\r\n" +
                                      "For help:\r\n" +
                                      "  >ImageTool /?";

                string HelpMessage = "Syntax:\r\n" +
                                     "   >ImageTool /[option] [file_location]* [mpxip]**\r\n" +
									 "      Notes:\r\n" +
                                     "       - [file_location] is relative to the\r\n" +
                                     "         current working directory.\r\n" +
									 "       - (*) denotes dependent console parameters in\r\n" +
									 "         Options, (**) is dependent on both.\r\n\r\n" +
                                     "Options:\r\n" +
                                     "   /? - Displays help\r\n" +
                                     "  */f - Displays the Min_FB_Version\r\n" +
                                     "  */h - Displays the entire Header\r\n" +
                                     "  */n - Displays the Model_Name\r\n" +
                                     "  */s - Displays the Min_Script_Version\r\n" +
                                     "  */t - Displays the Type\r\n" +
                                     "  */w - Reformats the EBO image file provided in [file_location]\r\n" +
                                     "        to a Wireshark readable '.pcapng' file. The resulting file\r\n" +
                                     "        will be placed in the current working directory, and will\r\n" +
                                     "        overwrite previous conversions.\r\n" +
									 " **/r - Sends all instructions contained within\r\n" +
                                     "        [file_location] to the BACnet listening port\r\n" +
                                     "    	   located at the IPv4 address in [mpxip].";

                if (Args.Length == 0 || Args.Length > 3)
                {
                    Console.WriteLine("ERROR - Too few/many arguments\r\n\r\n" + ErrorMessage);
                    Environment.Exit(24); //24 = Windows: ERROR_BAD_LENGTH
                }

                string Option = Args[0].ToLower();
                string Query = string.Empty;
                if (Args.Length == 1)
                {
                    if (Option == "/?")
                    {
                        Query = HelpMessage;
                    }
                    else
                    {
                        Query = String.Format("ERROR - You must specifiy [file_location] when using {0}\r\n\r\n" + ErrorMessage, Option);
                    }
                }
                else if (Args.Length == 2)
                {
                    if (Option == null || Args[1] == null)
                    {
                        Query = ErrorMessage;
                    }
                    else if (Option == "/?")
                    {
                        Query = HelpMessage;
                    }
					else if (Option == "/r")
					{
						Query = String.Format("ERROR - You must also specifiy [mpxip] when using {0}\r\n\r\n" + ErrorMessage, Option);
					}
                    else
                    {
                        FileInfo FInfo = new FileInfo(Args[1]);
                        if (!FInfo.Exists)
                        {
                            Query = String.Format("ERROR - The file provided does not exist: {0}\r\n", Args[1]);
                        }
                        else
                        {
                            switch (Option)
                            {
                                case "/n":
                                    Query = HeaderTag(0, Args[1]);
                                    break;
                                case "/s":
                                    Query = HeaderTag(1, Args[1]);
                                    break;
                                case "/f":
                                    Query = HeaderTag(2, Args[1]);
                                    break;
                                case "/t":
                                    Query = HeaderTag(3, Args[1]);
                                    break;
                                case "/h":
                                    Query = HeaderTag(5, Args[1]);
                                    break;
                                case "/w":
                                    Console.WriteLine("Extracting...");
                                    Query = Convert(Args[1]);
                                    break;
                                default:
                                    Query = String.Format("ERROR - Unrecognized option: {0}\r\n\r\n" + ErrorMessage, Option);
                                    break;
                            }
                        }
                    }
                }
				else
				{
					if (Option == null || Args[1] == null || Args[2] == null)
                    {
                        Query = "ERROR - Null argument\r\n" + ErrorMessage;
                    }
					else if (Option == "/?")
					{
						Query = HelpMessage;
					}
					else if (Option == "/r")
					{
						FileInfo FInfo = new FileInfo(Args[1]);
                        if (!FInfo.Exists)
                        {
                            Query = String.Format("ERROR - The file provided does not exist: {0}\r\n\r\n", Args[1]);
                        }
                        else if (!ValidateIPv4(Args[2]))
                        {
                            Query = String.Format("ERROR - Invalid IP address: {0}\r\n", Args[2].ToString());
                        }
                        Query = Replay(Args[1], Args[2]);
					}
					else
					{
						Query = String.Format("ERROR - Too many arguments provided for {0}\r\n\r\n" + ErrorMessage, Option);
					}
				}
                Console.WriteLine(Query);
            }
            
            /*
             * Puts file in buffer, then searches for and returns either the 
             * specified header value, or the entire header.
             * 
             * All values, except Type, are assumed to be strings, so some 
             * liberties are taken in parsing.
             * 
             * input:
             * int Param: Header value requested; 5 for entire header, or 0-3 for
             *            a specific value
             * string ImageFile: Image file location relative to current working
             *                   directory
             *                   
             * returns:
             * string Result: Specified header value/entire header
             */
            private static string HeaderTag(int Param, string ImageFile)
            {
                byte[] Buffer;
                try
                {
                    File.SetAttributes(ImageFile, FileAttributes.Normal);
                    Buffer = File.ReadAllBytes(ImageFile);
                }
                catch (DirectoryNotFoundException Ex)
                {
                    return Ex.ToString();
                }

                string Result = string.Empty, Tag = string.Empty;
                int TagNum = 0, TagLen = 0;
                for (int I = 3; TagNum < 4; I += TagLen)
                {
                    //Each value except type always a string, tagged at least 114 (0x72) 
                    //for a tag length of 1. TagLen is the difference of tag, 112
                    //(0x70), and one for the char set encode.
                    TagLen = Buffer[I] - 113;
                
                    if (TagLen == 4)
                    {
                        TagLen = Buffer[I + 1] - 1;
                        I++;
                    }
                    else if (TagLen < 0)
                    {
                        if (Buffer[I] == 16) Tag = "True";
                        else Tag = "False";
                    }
                    I += 2; //Skip char set encode

                    if (TagNum == 0) Tag = "Model_Name";
                    else if (TagNum == 1) Tag = "Min_Script_Version";
                    else if (TagNum == 2) Tag = "Min_FB_Version";

                    if (Param == 5)
                    {
                        if (TagNum == 3) Result += string.Format("Type: {0}\r\n", Tag);
                        else Result += string.Format("{0}: {1}\r\n", Tag, System.Text.Encoding.ASCII.GetString(Buffer, I, TagLen));
                    }
                    else if (TagNum == Param)
                    {
                        if (TagNum == 3) Result = string.Format("Type: {0}\r\n", Tag);
                        else Result = string.Format("{0}: {1}\r\n", Tag, System.Text.Encoding.ASCII.GetString(Buffer, I, TagLen));
                        break;
                    }
                    TagNum++;
                }
                return Result;  
            }

            /*
             * Reformats the NPDU instructions from the EBO image file provided to 
             * .pcapng readable format.
             * 
             * input:
             * string ImageFile: Image file location relative to current working
             *                   directory
             *                   
             * returns:
             * string "Finished": Message string for console output in Main()
             */
            private static string Convert(string ImageFile)
            {
                byte[] FileBuffer;
                try
                {
                    File.SetAttributes(ImageFile, FileAttributes.Normal);
                    FileBuffer = File.ReadAllBytes(ImageFile);
                }
                catch (Exception Ex)
                {
                    return Ex.ToString();
                }

                FileStream FS;
                string ConFile = Path.Combine(Directory.GetCurrentDirectory(), "IFConversion.pcapng").ToString();
                try
                {
                    if (File.Exists(ConFile))
                    {
                        File.SetAttributes(ConFile, FileAttributes.Normal);
                        File.Delete(ConFile);
                    }
                    FS = File.Create(ConFile);
                    File.SetAttributes(ConFile, FileAttributes.Normal);
                }
                catch (Exception Ex)
                {
                    return Ex.ToString();
                }

                //Pcap header
                byte[] PHeader = new byte[] {0xD4, 0xC3, 0xB2, 0xA1,    //Block Type
                                             0x02, 0x00,                //Block Length
                                             0x04, 0x00,                //Byte-Order Magic
                                             0x00, 0x00, 0x00, 0x00,    //Major Version
                                             0x00, 0x00, 0x00, 0x00,    //Minor Version
                                             0xFF, 0xFF, 0x00, 0x00,    //Section Length
                                             0x00, 0x00, 0x00, 0x00};   //Block Length
                FS.Write(PHeader, 0, PHeader.Length);

                //Frame header, 52 bytes
                byte[] FHeader = new byte[] {0x00, 0x00, 0x00, 0x00,  //UTC Timestamp
                                             0x00, 0x00, 0x00, 0x00,  //UTC Timestamp (fractions)
                                             0x00, 0x00, 0x00, 0x00,  //Frame Length #1
                                             0x00, 0x00, 0x00, 0x00,  //Frame Length #2
                                             0x02, 0x00,              //Null/Loopback
                                             0x08, 0x00,              //Ether Type
                                             0x45,                    //IP Protocol
                                             0x00,                    //IP Diff
                                             0x00, 0x00,              //IP Length (frame length + 30)
                                             0x00, 0x00,              //IP ID
                                             0x00,                    //IP Flags
                                             0x00,                    //IP Fragment
                                             0x80,                    //IP TTL
                                             0x11,                    //IP Protocol
                                             0x00, 0x00,              //IP Header Checksum
                                             0x00, 0x00, 0x00, 0x00,  //IP Source
                                             0x00, 0x00, 0x00, 0x00,  //IP Dest
                                             0xBA, 0xC0,              //IP Source Port
                                             0xBA, 0xC0,              //IP Dest Port
                                             0x00, 0x00,              //IP Length (frame length + 8)
                                             0x00, 0x00,              //IP Checksum
                                             0x81,                    //BVLC Type
                                             0x0A,                    //BVLC Function
                                             0x00, 0x00};             //BVLC Length (frame length + 2)

                byte[] Frame, Temp;
                bool First = true;
                int Len = 0, I = 0, Count = 0, SegI;
                while (I < FileBuffer.Length)
                {
                    Len = BitConverter.ToInt16(new byte[] { FileBuffer[I + 1], FileBuffer[I] }, 0);
                    I += 2;

                    Frame = new byte[Len];
                    for (SegI = 0; SegI < Len; SegI++)
                    {
                        Frame[SegI] = FileBuffer[I];
                        I++;
                    }

                    if (!First)
                    {
                        //Frame length tagging (including (EHeader.Length - 16))
                        Temp = BitConverter.GetBytes(Frame.Length + 36);
                        for (SegI = 0; SegI < Temp.Length; SegI++)
                        {
                            FHeader[SegI + 8] = Temp[SegI];
                            FHeader[SegI + 12] = Temp[SegI];
                        }

                        //IPLength field replacements
                        Temp = Shift(Frame.Length + 32);
                        FHeader[22] = Temp[0];
                        FHeader[23] = Temp[1];

                        Temp = Shift(Frame.Length + 12);
                        FHeader[44] = Temp[0];
                        FHeader[45] = Temp[1];

                        Temp = Shift(Frame.Length + 4);
                        FHeader[50] = Temp[0];
                        FHeader[51] = Temp[1];

                        FS.Write(FHeader, 0, FHeader.Length);
                        FS.Write(Frame, 0, Frame.Length);
                    }
                    else
                    {
                        Console.WriteLine("Converting...");
                        First = false;
                    }
                    Count++;
                }
                return String.Format("Finished Conversion - {0} instructions converted", Count);
        }
            
            /*
             * Uses bit shifting to split hex, generated from provided integer,
             * into two bytes in the returned array.
             * 
             * input:
             * int X: Integer to convert and shift
             * 
             * returns:
             * byte[] Swap: Bit shifted integer value in 2-byte hex
             */
            private static byte[] Shift(int X)
            {
                byte[] Swap = new byte[2];
                Swap[0] = (byte)(X >> 8);
                Swap[1] = (byte)X;
                return Swap;
            }

            /*
             * Sends the NPDUs contained within the provided EBO Image File to the specified
             * MPX device.
             * 
             * input:
             * string ImageFile: Image file location relative to current working
             *                   directory
             * string IP: String representation of targeted MPX device's IP address
             * 
             * return:
             * string "Finished": Message string for console output in Main()
             */
            private static string Replay(string ImageFile, string IP)
            {
                byte[] FileBuffer;
                try
                {
                    File.SetAttributes(ImageFile, FileAttributes.Normal);
                    FileBuffer = File.ReadAllBytes(ImageFile);
                }
                catch (Exception Ex)
                {
                    return Ex.ToString();
                }

                //Frame header,  bytes
                byte[] BVLCHeader = new byte[] {0x81,        //Type
                                                0x0A,        //Function
                                                0x00, 0x00}; //Length (frame length + 2)
                
                Socket Sender;
                try
                {
                    IPAddress MPXIP = IPAddress.Parse(IP);
                    IPEndPoint RemoteEP = new IPEndPoint(MPXIP, 47808); //47808 == 0xbac0
                    Sender = new Socket(MPXIP.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

                    try
                    {
                        Sender.Connect(RemoteEP);
                        Console.WriteLine("Socket connected to {0}", Sender.RemoteEndPoint.ToString());
                    }
                    catch (ArgumentNullException ANE)
                    {
                        return String.Format("ERROR - Argument Null Exception:\r\n{0}\r\n", ANE.ToString());
                    }
                    catch (SocketException SE)
                    {
                        return String.Format("ERROR - Socket Exception:\r\n{0}\r\n", SE.ToString());
                    }
                }
                catch (Exception EX)
                {
                    return String.Format("ERROR - Unexpected Exception:\r\n{0}\r\n", EX.ToString());
                }

                byte[] Frame, Temp;
                bool First = true;
                int Len = 0, I = 0, Count = 0, SegI;
                while (I < FileBuffer.Length)
                {
                    Len = BitConverter.ToInt16(new byte[] { FileBuffer[I + 1], FileBuffer[I] }, 0);
                    I += 2;

                    Frame = new byte[Len];
                    for (SegI = 0; SegI < Len; SegI++)
                    {
                        Frame[SegI] = FileBuffer[I];
                        I++;
                    }

                    if (!First)
                    {
                        Temp = Shift(Frame.Length + 4);
                        BVLCHeader[2] = Temp[0];
                        BVLCHeader[3] = Temp[1];
                        
                        try
                        {
                            Sender.Send(BVLCHeader.Concat(Frame).ToArray());
                        }
                        catch (ArgumentNullException ANE)
                        {
                            return String.Format("ERROR - Argument Null Exception:\r\n{0}\r\n", ANE.ToString());
                        }
                        catch (SocketException SE)
                        {
                            return String.Format("ERROR - Socket Exception:\r\n{0}\r\n", SE.ToString());
                        }
                        catch (Exception EX)
                        {
                            return String.Format("ERROR - Unexpected Exception:\r\n{0}\r\n", EX.ToString());
                        }
                    }
                    else
                    {
                        Console.WriteLine("Sending...");
                        First = false;
                    }
                    Count++;
                    Thread.Sleep(100); //Time interval for MPX to receive messages
                }
                Console.WriteLine("Closing connection...");
                Sender.Shutdown(SocketShutdown.Both);
                Sender.Close();

                return String.Format("Finished Replay - {0} instructions sent", Count);
        }
            
            /*
             * Simple helper method to verify whether or not the provided IP
             * address is valid, via some string checks.
             * 
             * input:
             * string IP: potential IP address represented in string format
             * 
             * returns:
             * bool Valid: True if supplied IP is valid, false if not
             */
            private static bool ValidateIPv4(string IP)
            {
                if (String.IsNullOrWhiteSpace(IP)) return false;

                string[] Splits = IP.Split('.');
                if (IP.Split('.').Length != 4) return false;

                byte Temp;
                return Splits.All(r => byte.TryParse(r, out Temp));
            }
		}
    }