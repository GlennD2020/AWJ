using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dms_control_v3
{
    static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            GlobalConfigurations configurations = new GlobalConfigurations();
            foreach (string s in args)
            {
                if ( s.Contains("dgmessage")==true )
                {
                    configurations.bDgMessage = true;
                }
                if ( s.Contains("user")==true )
                {
                    configurations.iMode = Mode.User;
                }
                else if ( s.Contains("advanced")==true )
                {
                    configurations.iMode = Mode.Advanced;
                }
                else if ( s.Contains("debug")==true )
                {
                    configurations.iMode = Mode.Debug;
                }
            }

            Application.EnableVisualStyles ( );
            Application.SetCompatibleTextRenderingDefault ( false );
            Application.Run ( new FmMain ( configurations ) );
        }
    }

    /// <summary>
    /// Режим отладки.
    /// </summary>
    public enum Mode : int
    {
        User     = 0,   //  Пользовательский режим
        Advanced = 1,   //  Инженерный режим
        Debug    = 2,   //  Отладочный режим
    }

    /// <summary> Класс глобальных настроек. </summary>
    public class GlobalConfigurations
    {
        /// <summary> Режим отладки. </summary>
        public bool bDgMessage;
        /// <summary> Режим запуска. </summary>
        public Mode iMode;

        /// <summary> Конструктор класса. </summary>
        public GlobalConfigurations ()
        {
            bDgMessage  = false;
            iMode       = Mode.User;
        }
    }
}
