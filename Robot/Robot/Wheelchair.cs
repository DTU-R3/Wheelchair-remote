using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Robot
{
    class Wheelchair
    {
        #region Variables

        // Wheelchair configuration, all in cm
        static public double x = 0;
        static public double y = 0;
        static public double theta = 0;             // In degree

        #endregion

        #region Initialise

        internal static void Init()
        {
            x = 0;
            y = 0;
            theta = 0;
        }

        #endregion

        #region Wheelchair control

        // Stop the wheelchair
        internal static void Stop()
        {      
            if (MainWindow.HWPort.IsOpen)
            {
                try
                {
                    MainWindow.HWDataSend("DK0110001000");
                }
                catch (Exception)
                {
                    
                }
            }
        }

        #endregion
    }
}
