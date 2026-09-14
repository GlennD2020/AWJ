/**
 * @file:       ProjectConfiguration.cs
 * @project:    Configuration Class.
 * @author:     Kirillov A.V.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace nsAlexKir
{
    /// <summary> Класс для работы с конфигурациями проекта. </summary>
    /// <typeparam name="Type"> Тип класса конфигурации проекта. </typeparam>
    public class ProjectConfiguration<Type>
    {
        /// <summary> Загрузка конфигурации из файла. </summary>
        /// <param name="pathToFile"> Путь к файлу. </param>
        /// <returns> Объект для сохранения конфигурации. </returns>
        public static Type LoadConfigurationFromXMLfile     ( String pathToFile )
        {
            try
            {
                FileStream fsFile = new FileStream(pathToFile, FileMode.Open);
                XmlSerializer xmlser = new XmlSerializer(typeof(Type));
                Type cfg = (Type)xmlser.Deserialize(fsFile);
                fsFile.Close();
                return cfg;
            }
            catch
            {
                return default(Type);
            }
        }

        /// <summary>Сохранение конфигурации в файл. </summary>
        /// <param name="pathToFile"> Путь к файлу. </param>
        /// <param name="obj"> Объект с настройками проекта. </param>
        public static void SaveConfigurationToXMLfile       ( String pathToFile, object obj )
        {
            try
            {
                FileStream fsFile = new FileStream ( pathToFile, FileMode.Create );
                if (obj.GetType() == typeof(Type))
                {
                    XmlSerializer xmlser = new XmlSerializer(typeof(Type));
                    xmlser.Serialize(fsFile, (Type)obj);
                }
                fsFile.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

    }
}
