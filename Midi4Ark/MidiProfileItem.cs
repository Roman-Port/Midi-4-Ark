using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Midi4Ark
{
    class MidiProfileItem
    {
        public bool isDown = false; //Bodge fix
        public Midi.Pitch pitch;
        public Keys key;

        public MidiProfileItem(Midi.Pitch _pitch, Keys _key)
        {
            key = _key;
            pitch = _pitch;
        }
    }
}
