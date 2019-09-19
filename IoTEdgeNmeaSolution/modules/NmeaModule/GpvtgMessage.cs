using System;

namespace svelde.nmea.parser
{
    public class GpvtgMessage : GnvtgMessage
    {
        public override string GetIdentifier()
        {
            return "$GPVTG";
        }

        public override void Parse(string nmeaLine)
        {
            base.Parse(nmeaLine);
        }
    }
}

