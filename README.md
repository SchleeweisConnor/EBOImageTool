EBO Image Tool
-------------------

About:
	ImageTool.exe is a command-line utility that takes an EBO image 
	file, and a desired option, and returns either a result to the 
	command window, Wireshark (.pcapng) converted version of 
	the image file to the current working directory, or the tool will
	send all NPDUs contained within the specified EBO Image File to a
	targeted MPX.

	Source code is included in ImageTool.cs

Command-line Syntax:

	Note: ( *[...] ) indicates optional argument

	Wireshark Conversion:
	  >ImageTool /w [file_location] *[new_name]

	Replay:
	  >ImageTool /r [file_location] [mpxip]

	Display Header/Header Tags:
	  >ImageTool /[f/h/n/s/t] [file_location]

	Display Help:
	  >ImageTool /?

Command-line Arguments:
	
	[option]: listed below, decides the operation taken
	
	[file_location]: path to EBO image file, may be either absolute, 
			 or relative to the current working directory
	
	[new_name]: optionally used with [ /w ]; a name provided here will
			 replace the default conversion file name: "IFConversion"
	
	[mpxip]: used with [ /r ]; IPv4 address of targeted MPX device

Command-line Options:                	Output:

    	/? - Help                       Char_String

     	/f - Min_FB_Version             Char_String

     	/h - Header (entire)            Char_String

     	/n - Model_Name                 Char_String

     	/s - Min_Script_Version         Char_String

     	/t - Type                       Boolean

     	/w - Reformat to Wireshark      (.pcapng) file
             **Note: Reformats the EBO image file provided in [file_location]
                     to a Wireshark readable '.pcapng' file. The resulting
                     file will be placed in the current working directory,
		     and will overwrite previous conversions.
	
	/r - Replay Instructions	APDUs -> MPX
	     **Note: Sends all instructions contained within [file_location]
		     to the BACnet listening port located at the IPv4 address
		     specified in [mpxip].
