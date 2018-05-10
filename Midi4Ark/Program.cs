using Midi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Midi4Ark
{
    class Program
    {
        [DllImport("user32.dll")]
        static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        static extern ushort MapVirtualKey(int wCode, int wMapType);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        static void Main(string[] args)
        {
            StartMidi();
            state = ProgramState.Setup;
            if (!LoadProfile())
            {
                PromptForKey();
            } else
            {
                state = ProgramState.Ready;
                Console.Clear();
                Console.WriteLine("Loaded and ready!");
            }

            
            


        }

        public static string SaveProfile()
        {
            Random rand = new Random();
            string filename = Environment.GetFolderPath(Environment.SpecialFolder.Desktop).TrimEnd('\\')+"\\Midi4Ark Profile "+rand.Next(0,int.MaxValue)+".midi4ark";
            //save
            string output = "";
            foreach(MidiProfileItem item in activeKeys)
            {
                output += (int)item.key + "," + (int)item.pitch + "|";
            }
            output = output.TrimEnd('|');
            System.IO.File.WriteAllText(filename, output);
            return filename;
        }
        public static bool LoadProfile()
        {
            activeKeys.Clear();
            Console.Clear();
            Console.WriteLine("Type in a config file location. Leave it blank if you'd like to create a new one.");
            string input = Console.ReadLine();
            if(input.Length<2)
            {
                //Return false because it failed.
                return false;
            }
            //Load
            string file = System.IO.File.ReadAllText(input);
            string[] data = file.Split('|');
            foreach(string d in data)
            {
                string[] dd = d.Split(',');
                int key = int.Parse(dd[0]);
                int pitch = int.Parse(dd[1]);
                //Add it
                MidiProfileItem item = new MidiProfileItem((Midi.Pitch)pitch, (Keys)key);
                item.isDown = false;
                activeKeys.Add(item);
            }
            return true;
        }

        public static void StartMidi()
        {
            // Create a clock running at the specified beats per minute.
            int beatsPerMinute = 180;
            Clock clock = new Clock(beatsPerMinute);

            foreach (InputDevice id in InputDevice.InstalledDevices)
            {
                Console.WriteLine(id.Name);
            }

            // Prompt user to choose an input device (or if there is only one, use that one).
            InputDevice inputDevice = InputDevice.InstalledDevices[InputDevice.InstalledDevices.Count - 1];
            try
            {
                if (inputDevice != null)
                {
                    inputDevice.Open();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Failed to open MIDI. Is another program using it?\r\n"+ex.Message);
                Console.ReadLine();
                return;
            }

            Arpeggiator arpeggiator = new Arpeggiator(inputDevice, clock);
            clock.Start();
            if (inputDevice != null)
            {
                inputDevice.StartReceiving(clock);
            }
        }

        public static ProgramState state = ProgramState.Stopped;

        public static void HandleMidi(Midi.Pitch pitch)
        {
            //Handles ALL incoming requests.
            //Check if we're okay to run the commands.
            if(state==ProgramState.Setup)
            {
                //Change to the setup function
                HandleMidiSetup(pitch);
                return;
            }
            if(state != ProgramState.Ready)
            {
                return;
            }
            //Look up the key
            MidiProfileItem item = FindItemByMidiPitch(pitch);
            //Check if it's real
            if(item==null)
            {
                //Invalid
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Key " + pitch.ToString() + " pressed, but no valid key was found!");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }
            //Valid key! Toggle it
            if(item.isDown)
            {
                //Pressed. Release it
                StopKey(item.key);
            } else
            {
                //Not pressed. Press it
                SendKey(item.key);
            }
            //Print
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Key " + pitch.ToString() + " changed. Key " + item.key + " is toggled. Key is now down? " + (!item.isDown).ToString());
            //Toggle it
            item.isDown = !item.isDown;
        }

        public static void HandleMidiSetup(Midi.Pitch pitch)
        {
            //This is run when a key is pressed in setup mode.
            //Check if we're waiting for a key, or if the user is just pressing keys because they feel like it
            if(waitingForKeyInput)
            {
                //Check if that key already exists.
                if(FindItemByMidiPitch(pitch)!=null)
                {
                    //It exists! Ignore.
                    return;
                }
                waitingForKeyInput = false;
                //Okay. Deal with it.
                MidiProfileItem item = new MidiProfileItem(pitch, midiSetupBufferKey);
                activeKeys.Add(item);
                
                bool ok = false;
                bool cont=false;
                while(!ok)
                {
                    Console.Clear();
                    Console.WriteLine("Done!\r\nWould you like to continue? [Y/N]\r\n");
                    string input = Console.ReadLine();
                    if(input.ToLower()=="y")
                    {
                        ok = true;
                        cont = true;
                    }
                    if (input.ToLower() == "n")
                    {
                        ok = true;
                        cont = false;
                    }
                }
                //Check if we should continue
                if(!cont)
                {
                    //Just change the status to good and continue
                    Console.Clear();
                    Console.WriteLine("Ready!");
                    Console.WriteLine("Saved config to " + SaveProfile() + "!");
                    state = ProgramState.Ready;
                    //Also reverse the last key to be pressed because jank
                    activeKeys[activeKeys.Count - 1].isDown = true;
                    
                    return;
                }
                //Ask again
                PromptForKey();
            }
        }

        public static void PromptForKey()
        {
            //This will ask the user to press a key on the midi keyboard after it prompts for a key to use.
            string input = "";
            Keys key;
            while (Enum.TryParse<Keys>(input,out key)==false)
            {
                //Keep prompting
                Console.Clear();
                Console.WriteLine("===[ Setup 1/2 ]===");
                Console.WriteLine("\r\nPlease type in the name of the key on the keyboard.\r\nExamples:\r\n   Space\r\n   A\r\n   Delete\r\n");
                input = Console.ReadLine();
                
                if(input.Length>0)
                {
                    //Change the first letter to upper case
                    string tmp = input.Substring(1);
                    input=input.Substring(0, 1).ToUpper() + tmp;
                }
            }
            //We now have the key to use. Prompt the user to press a key on the midi keyboard, then stop here. We'll get a callback when they're done.
            waitingForKeyInput = true;
            midiSetupBufferKey = key;
            Console.Clear();
            Console.WriteLine("===[ Setup 2/2 ]===");
            Console.WriteLine("\r\nPlease press the key on the MIDI keyboard you'd like to use for \""+key.ToString().ToLower()+"\".\r\n");
        }

        public static Keys midiSetupBufferKey;
        public static bool waitingForKeyInput = false;



        public static List<MidiProfileItem> activeKeys = new List<MidiProfileItem>();

        public static MidiProfileItem FindItemByMidiPitch(Midi.Pitch pitch)
        {
            //Search for this
            foreach(MidiProfileItem p in activeKeys)
            {
                //Check
                if(pitch==p.pitch)
                {
                    return p;
                }
            }
            //Not valid. Just ignore it
            return null;
        }

        public static void SendKey(Keys key)
        {
            keybd_event((byte)key, 0, 0x0001 | 0, 0);
        }

        public static void StopKey(Keys key)
        {
            keybd_event((byte)key, 0, 0x0001 | 0x0002, 0);
        }


    }

    class Arpeggiator
    {
        //Handles midi
        public Arpeggiator(InputDevice inputDevice, Clock clock)
        {
            this.inputDevice = inputDevice; this.clock = clock;

            if (inputDevice != null)
            {
                inputDevice.NoteOn += new InputDevice.NoteOnHandler(this.NoteOn);
                inputDevice.NoteOff += new InputDevice.NoteOffHandler(this.NoteOff);
            }
        }

        public void NoteOn(NoteOnMessage msg)
        {
            Program.HandleMidi(msg.Pitch);
        }

        public void NoteOff(NoteOffMessage msg)
        {
            Program.HandleMidi(msg.Pitch);
        }

        private InputDevice inputDevice;
        private Clock clock;
    }

    enum ProgramState
    {
        Ready,
        Stopped,
        Setup
    }
}
