using Newtonsoft.Json; // NuGet: Newtonsoft.Json
using Newtonsoft.Json.Linq; // Para manipular JSON
using System;
using System.Messaging;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Xml.Linq; // Para manipular XML

namespace TranslatorService
{
    class Program
    {
        //  COLAS DE ORIGEN 
        static string colaOrigenXML = @".\Private$\jav_xml_pagos";
        static string colaOrigenWeb = @".\Private$\jav_web_pagos";

        //  COLA DE DESTINO 
        static string colaDestino = @".\Private$\jav_datatype_pagos";

        static void Main(string[] args)
        {
            Console.WriteLine("--- TRADUCTOR UNIVERSAL (TEAM JAV) ---");
            Console.WriteLine($"Destino final: {colaDestino}");

            // Crear la cola de destino si no existe
            if (!MessageQueue.Exists(colaDestino))
            {
                MessageQueue.Create(colaDestino, true); // Transaccional
                Console.WriteLine("[CONFIG] Cola 'jav_datatype_pagos' creada.");
            }

            Console.WriteLine("Iniciando servicios de traducción...");

            // Lanzamos dos hilos: uno vigila XML, otro vigila WEB.
            Thread hiloXML = new Thread(ProcesarXML);
            Thread hiloWeb = new Thread(ProcesarWeb);

            hiloXML.Start();
            hiloWeb.Start();
        }

        // --- TRADUCTOR 1: DE XML A CANÓNICO ---
        static void ProcesarXML()
        {
            Console.WriteLine(" [XML] Vigilando cola XML...");
            if (!MessageQueue.Exists(colaOrigenXML)) { Console.WriteLine(" [ERROR] No existe cola XML"); return; }

            using (MessageQueue cola = new MessageQueue(colaOrigenXML))
            {
                cola.Formatter = new XmlMessageFormatter(new String[] { "System.String,mscorlib" });

                while (true)
                {
                    try
                    {
                        // Esperamos mensaje 
                        Message mensaje = cola.Receive();
                        string xmlBody = mensaje.Body.ToString();

                        // 1. TRADUCIR
                        XElement xml = XElement.Parse(xmlBody);

                        // Creamos el objeto Canónico (Estandarizamos los nombres)
                        var pagoCanonico = new
                        {
                            origen = "SUCURSAL",
                            // Buscamos los valores dentro del XML 
                            monto = xml.Element("Monto")?.Value ?? "0",
                            rut_cliente = xml.Element("Id")?.Value ?? "S/N", 
                            fecha = DateTime.Now.ToString("yyyy-MM-dd")
                        };

                        string jsonFinal = JsonConvert.SerializeObject(pagoCanonico);

                        // 2. ENVIAR A DESTINO
                        EnviarAlCentral(jsonFinal, "Pago Canonico (Ex-XML)");
                        Console.WriteLine(" [XML] -> Traducido y enviado al Central.");
                    }
                    catch (Exception) { /* Ignoramos timeouts de cola vacía */ }
                }
            }
        }

        // --- TRADUCTOR 2: DE WEB A CANÓNICO ---
        static void ProcesarWeb()
        {
            Console.WriteLine(" [WEB] Vigilando cola Web...");
            if (!MessageQueue.Exists(colaOrigenWeb)) { Console.WriteLine(" [ERROR] No existe cola Web"); return; }

            using (MessageQueue cola = new MessageQueue(colaOrigenWeb))
            {
                cola.Formatter = new XmlMessageFormatter(new String[] { "System.String,mscorlib" });

                while (true)
                {
                    try
                    {
                        Message mensaje = cola.Receive();
                        string jsonBody = mensaje.Body.ToString();

                        // 1. TRADUCIR
                        JObject jsonOriginal = JObject.Parse(jsonBody);

                        // Mapeamos los campos del profe a los nuestros
                        var pagoCanonico = new
                        {
                            origen = "WEB",
                            // El simulador web suele enviar "amount", "id", etc.
                            monto = jsonOriginal["amount"]?.ToString() ?? jsonOriginal["monto"]?.ToString() ?? "0",
                            rut_cliente = jsonOriginal["clientId"]?.ToString() ?? jsonOriginal["clienteId"]?.ToString() ?? "Anonimo",
                            fecha = DateTime.Now.ToString("yyyy-MM-dd")
                        };

                        string jsonFinal = JsonConvert.SerializeObject(pagoCanonico);

                        // 2. ENVIAR A DESTINO
                        EnviarAlCentral(jsonFinal, "Pago Canonico (Ex-Web)");
                        Console.WriteLine(" [WEB] -> Traducido y enviado al Central.");
                    }
                    catch (Exception) { }
                }
            }
        }

        static void EnviarAlCentral(string json, string etiqueta)
        {
            using (MessageQueue colaDestino = new MessageQueue(Program.colaDestino))
            {
                Message msg = new Message();
                msg.Label = etiqueta;
                msg.Body = json;

                using (MessageQueueTransaction tx = new MessageQueueTransaction())
                {
                    tx.Begin();
                    colaDestino.Send(msg, tx);
                    tx.Commit();
                }
            }
        }
    }
}