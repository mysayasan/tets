using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;

namespace Tets
{
    public class XMLUtils
    {
        public static void DeserializeXMLToObject<T>(out T item, string filepath)
        {
            item = default(T);
            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(T));
                using (StreamReader rd = new StreamReader(filepath))
                {
                    item = (T)xs.Deserialize(rd);
                    rd.Close();
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public static void SerializeObjectToXML<T>(T item, string filepath)
        {
            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(T));
                using (StreamWriter wr = new StreamWriter(filepath))
                {
                    xs.Serialize(wr, item);
                    wr.Close();
                }
            }
            catch { }
            {
                //throw new Exception(e.Message);
            }
        }
    }
}
