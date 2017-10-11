using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Robot
{
    class JoyStick
    {
        // Init Joystick      
        static public int speedLimit = 60;
        static public int minSpeed = 30;
        static public int maxSpeed = 300;
        static public double rightX = 0.0;
        static public double rightY = 0.0;
        static public int leftSpeed = 0;
        static public int rightSpeed = 0;
    }
}
