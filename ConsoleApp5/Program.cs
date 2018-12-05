using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using PcapDotNet.Core;
using PcapDotNet.Core.Extensions;
using PcapDotNet.Packets;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp5
{
    class Program
    {
        static Pregunta preguntaTotal = new Pregunta();
        delegate bool articulo(string str);
        static void Main(string[] args)
        {
            /* DESCOMENTAR ESTO PARA TESTEAR PREGUNTAS
            Pregunta p = new Pregunta { Id = "1", Titulo= "De que son las nuevas materias creadas por la Universidad de Binghamtom?", Opcion1= "Huracanes", Opcion2= "Tornados", Opcion3= "Tempestades" };
            // CargarPregunta(p, new List<int> { 0, 10, 0 });
            //var lista = BuscarPregunta("Cual de estas novelas no tuvo segunda parte?", "El jardin del Eden", "Don quijote", "Ensayo sobre la ceguera");
            // Retrieve the device list from the local machine
            var resultado = BuscarPregunta("Cual de estos libros fue escrito por el argentino hernan vanoli?", "Una misma noche", "Pyongyang", "Ninguno de los dos");
            CargarPregunta(p, resultado);*/
            IList<LivePacketDevice> allDevices = LivePacketDevice.AllLocalMachine;

            if (allDevices.Count == 0)
            {
                //Console.WriteLine("No interfaces found! Make sure WinPcap is installed.");
                return;
            }

            // Print the list
            for (int i = 0; i != allDevices.Count; ++i)
            {
                LivePacketDevice device = allDevices[i];
            
                /* Console.Write((i + 1) + ". " + device.Name);
                 if (device.Description != null)
                     Console.WriteLine(" (" + device.Description + ")");
                 else
                     Console.WriteLine(" (No description available)");*/
            }
            string s = "--------------Esperando preguntas--------------";
            Console.WriteLine(s);
            int deviceIndex = 0;
            do
            {
                //Console.WriteLine("Enter the interface number (1-" + allDevices.Count + "):");
                string deviceIndexString = "1";
                if (!int.TryParse(deviceIndexString, out deviceIndex) ||
                    deviceIndex < 1 || deviceIndex > allDevices.Count)
                {
                    deviceIndex = 0;
                }
            } while (deviceIndex == 0);

            // Take the selected adapter
            PacketDevice selectedDevice = allDevices[deviceIndex - 1];

            // Open the device
            using (PacketCommunicator communicator =
                selectedDevice.Open(65536,                                  // portion of the packet to capture
                                                                            // 65536 guarantees that the whole packet will be captured on all the link layers
                                    PacketDeviceOpenAttributes.None, // promiscuous mode
                                    1000))                                  // read timeout
            {
               // Console.WriteLine("Listening on " + selectedDevice.Description + "...");

                // start the capture
                communicator.ReceivePackets(0, PacketHandler);
            }
        }

        // Callback function invoked by Pcap.Net for every incoming packet
        private static void PacketHandler(Packet packet)
        {
            if ((packet.Ethernet.IpV4.Source.ToString()=="190.221.5.221")&& packet.Buffer.Length>180)
            {
                string contenido = Encoding.UTF8.GetString(packet.Buffer);
                if (!contenido.Contains("questionFinished"))
                {
                    try
                    {
                        Pregunta preg = ParsearPacket(contenido);
                        if (preg.Titulo!=preguntaTotal.Titulo)
                        {
                            preguntaTotal = preg;
                            var resultado = BuscarPregunta(preg.Titulo, preg.Opcion1, preg.Opcion2, preg.Opcion3);
                            CargarPregunta(preg, resultado);
                        }
                       
                    }
                    catch 
                    {
                        int a = 1;
                    }
                   
                }        
            }
        }

         private static Pregunta ParsearPacket(string contenido)
        {
           Pregunta p = new Pregunta();
           var jsonString = "{\"type" + contenido.Split(new string[] { "{\"type" }, StringSplitOptions.None)[1];
           JObject json= JObject.Parse(jsonString);
           p.Id = ((JValue)json["id"]).Value.ToString();
           p.Titulo = ((JValue)json["payload"]["data"]["questionStarted"]["quiz"]).Value.ToString();
           p.Opcion1 = ((JValue)json["payload"]["data"]["questionStarted"]["options"][0]).Value.ToString();
           p.Opcion2 = ((JValue)json["payload"]["data"]["questionStarted"]["options"][1]).Value.ToString();
           p.Opcion3 = ((JValue)json["payload"]["data"]["questionStarted"]["options"][2]).Value.ToString();
           return p;
        }

        private static List<Dictionary<string,int>> BuscarPregunta(string pregunta, string opcion1,string opcion2,string opcion3)
        {
            pregunta = pregunta.Replace("\"", "");
            List<string> listaOpciones = new List<string>{ SacarAcentos(opcion1.ToLower()), SacarAcentos(opcion2.ToLower()), (SacarAcentos(opcion3.ToLower())) };
            List<Dictionary<string, int>> listaRespuesta = new List<Dictionary<string, int>>();
            var link = "https://google.com.ar/search?q={0}&oq={0}+&aqs=chrome..69i57j0l5.3305j0j7&sourceid=chrome&ie=UTF-8";
            Process.Start(String.Format(link, pregunta));

            var client = new RestClient("https://www.googleapis.com/customsearch/v1?key={TU-KEY-DE-GOOGLE}&cx=009131617097369123982:z5bool75i68&q=" + pregunta);
            var request = new RestRequest("", Method.GET);
            IRestResponse response = client.Execute(request);
            var content = SacarAcentos(response.Content.ToLower());
            Task tarea = new Task(
            () =>
                    {
                        string titulo = "";
                        var wikipediaLista = BuscarWikipedia(response.Content, listaOpciones[0], listaOpciones[1], listaOpciones[2],ref titulo);
                        ImprimirBloque(titulo, wikipediaLista);
                    }
                 );
            tarea.Start();
            var SinRepetidos = SacarRepetidos(listaOpciones);
            List<int> listaAux = new List<int>();
            for (int i = 0; i < listaOpciones.Count; i++)
            {
                listaRespuesta.Add(BuscarPalabra(content, listaOpciones[i], SinRepetidos[i]));
            }
            return listaRespuesta;
        }

        private static string SacarAcentos(string stringx)
        {
            return stringx.Replace("á", "a")
                          .Replace("Á", "A")
                          .Replace("é", "e")
                          .Replace("É", "E")
                          .Replace("í", "i")
                          .Replace("Í", "I")
                          .Replace("ó", "o")
                          .Replace("Ó", "O")
                          .Replace("ú", "u")
                          .Replace("Ú", "U"); // raw content as string
        }

        private static void CargarPregunta(Pregunta P,List<Dictionary<string,int>> respuesta)
        {
            Console.Clear();
            Console.WriteLine(P.Titulo);
            Console.WriteLine("");
            ImprimirLista(respuesta);
        }

        //Busca en wikipedia
        private static List<Dictionary<string, int>> BuscarWikipedia(string respuesta, string opcion1, string opcion2, string opcion3,ref string titulo)
        {

            List<Dictionary<string, int>> lista = new List<Dictionary<string, int>>();
            try
            {
                JObject json = JObject.Parse(respuesta);
                foreach (JObject item in json["items"])
                {
                    string link = ((JValue)item["link"]).Value.ToString();
                    if (link.ToLower().Contains("wikipedia"))
                    {
                        var client = new RestClient(link);
                        var request = new RestRequest("", Method.GET);
                        client.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/65.0.3325.181 Safari/537.36";
                        request.AddHeader("Cache-Control", "max-age=0");
                        request.AddHeader("Upgrade-Insecure-Requests", "1");
                        request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
                        request.AddHeader("Accept-Encoding", "gzip, deflate");
                        request.AddHeader("Accept-Language", "es-ES,es;q=0.9,en-US;q=0.8,en;q=0.7");
                        IRestResponse response = client.Execute(request);
                        var content = SacarAcentos(response.Content.ToLower());
                        List<string> listaOpciones = new List<string> { SacarAcentos(opcion1.ToLower()), SacarAcentos(opcion2.ToLower()), SacarAcentos(opcion3.ToLower()) };
                        var SinRepetidos=SacarRepetidos(listaOpciones);
                       List<int> listaAux = new List<int>();
                        for (int i = 0; i < listaOpciones.Count; i++)
                        {
                            lista.Add(BuscarPalabra(content, listaOpciones[i], SinRepetidos[i]));
                        }
                          
                      
                     titulo = ((JValue)item["title"]).Value.ToString().Split(',')[0];
                     return lista;
                     }
                }
                return new List<Dictionary<string, int>> {  };
            }
            catch 
            {
                return new List<Dictionary<string, int>> { };
              
            }

         }

        //Devuelve un diccionario con la palabra buscada y la cantidad de veces que aparece
        private static Dictionary<string,int> BuscarPalabra(string content, string opcion,string opcionEditada)
        {
            opcion = opcion.ToLower();
            Dictionary<string, int> dict = new Dictionary<string, int>();
            if (opcion.Split(' ').Length>1)
            {
                dict.Add(opcion, content.Split(new string[] { opcion }, StringSplitOptions.None).Length - 1);
            }
            articulo contieneArticulo = x => x=="el"
                                          || x == "la" 
                                          || x == "los"
                                          || x == "las"
                                          || x == "un"
                                          || x == "uno"
                                          || x == "una"
                                          || x == "unas"
                                          || x == "unos"
                                          || x == "lo"
                                          || x == "a"
                                          || x == "del"
                                          || x == "de"
                                          || x == "en"
                                          || x == "no"
                                          || x == "se"
                                          || x == "y"
                                          || x == "al"
                                          || x == "por";
            var lista = opcionEditada.Split(' ');
            foreach (var item in lista)
            {
                int cont = 0;
                if (!contieneArticulo(item))
                {
                        foreach (var miniContent in content.Replace(".","")
                                                           .Replace(",", "")
                                                           .Replace("\"", "")
                                                           .Replace("{", "")
                                                           .Replace("}", "")
                                                           .Replace(";", "")
                                                           .Replace("\n", "")
                                                           .Replace("\t", "")
                                                           .Replace("\r", "")
                                                           .Replace("[", "")
                                                           .Replace("]", "")
                                                           .Split(new string[] { " ", ">" }, StringSplitOptions.None))
                        {
                        if (miniContent.Contains("pyongyang"))
                        {
                            int a = 1;
                        }
                            if (miniContent.Trim().Replace("title=","").Replace("“","").Replace("”", "").Replace("\"", "") == item.Trim())
                            {
                                cont++;
                            }
                        }

                    try
                    {
                        dict.Add(item, cont);
                    }
                    catch 
                    {

                    }
               
                }
            }
            return dict;
        }

        //Saca los elementos repetidos entre todas las opciones
       private static List<string> SacarRepetidos(List<string> listaOpciones)
        {
            List<List<string>> lista = new List<List<string>>();
            List<string> listaAux = new List<string>();
            foreach (var opcion in listaOpciones)
            {
                lista.Add(opcion.Split(' ').ToList().ConvertAll(x=>x.Trim()));
            }

            //Agarro los items iguales entre las listas
            foreach (var listaOpcion1 in lista)
            {
                foreach (var listaOpcion2 in lista)
                {
                    var intercept = listaOpcion1.Intersect(listaOpcion2);
                    if (intercept.Count()!= listaOpcion2.Count)
                    {
                        listaAux.AddRange(intercept);
                    }
                }
                listaAux = listaAux.Distinct().ToList();//Saco lo items repetidos
            }

            //Elimino los items iguales entre las listas
            foreach (var eliminador in listaAux)
            {
                foreach (var item in lista)
                {
                    if (item.Contains(eliminador))
                    {
                        item.Remove(eliminador);
                    }
                }
            }

            listaOpciones = new List<string>();

            foreach (var listaOpcion in lista)
            {
                string aux = "";
                foreach (var item in listaOpcion)
                {
                    aux = aux+" " + item;
                }
                listaOpciones.Add(aux.Trim());
            }
            return listaOpciones;
        }

        //Poner un string con las primeras letras en may
        private static string FormatSentence(string source)
        {
            var words = source.Split(' ').Select(t => t.ToCharArray()).ToList();
            words.ForEach(t =>
            {
                for (int i = 0; i < t.Length; i++)
                {
                    t[i] = i.Equals(0) ? char.ToUpper(t[i]) : char.ToLower(t[i]);
                }
            });
            return string.Join(" ", words.Select(t => new string(t))); ;
        }

        private static void ImprimirBloque(string nombre, List<Dictionary<string, int>> lista)
        {
            if (lista.Count > 0)
            {
                Console.WriteLine("");
                Console.WriteLine("     " + nombre + "     ");
                Console.WriteLine("");
                ImprimirLista(lista);
            }

        }

        private static void ImprimirLista(List<Dictionary<string, int>> lista)
        {
            foreach (var dict in lista)
            {
                Console.WriteLine("-----------------");
                foreach (var item in dict)
                {
                    string show = (item.Key.Split(' ').Length > 1) ? (item.Value > 0) ? " <--" : "" : "";
                    string stt = (FormatSentence(item.Key) + ":                       ").Substring(0, 19) + "  {0}" + show;
                    Console.WriteLine(String.Format(stt, item.Value));
                }
            }
            Console.WriteLine("-----------------");
        }
    }
 }

