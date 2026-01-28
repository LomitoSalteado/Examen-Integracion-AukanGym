using System;
using System.IO;
using System.Messaging; // Esta es la referencia que agregamos recién
using System.Xml.Linq;  // Para leer XML fácil
using System.Threading; // Para las pausas

namespace AdapterXML
{
    class Program
    {

        static string jav = @".\Private$\jav_xml_pagos";

        static string rutaCarpeta = @"C:\AukanGym\PagosXML";

        static void Main(string[] args)
        {
            Console.WriteLine("--- INICIANDO ADAPTER XML ---");
            Console.WriteLine($"Vigilando: {rutaCarpeta}");
            Console.WriteLine($"Enviando a: {jav}");

            // Verificamos si la carpeta existe. Si no, la creamos nosotros para evitar errores.
            if (!Directory.Exists(rutaCarpeta))
            {
                Console.WriteLine($"[AVISO] La carpeta {rutaCarpeta} no existía. Creándola...");
                Directory.CreateDirectory(rutaCarpeta);
            }

            // Bucle infinito: El programa nunca duerme, siempre vigila.
            while (true)
            {
                try
                {
                    BuscarArchivos();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }

                // Descansar 2 segundos antes de volver a mirar
                Thread.Sleep(2000);
            }
        }

        static void BuscarArchivos()
        {
            // Buscamos cualquier archivo .xml
            string[] archivos = Directory.GetFiles(rutaCarpeta, "*.xml");

            foreach (var archivo in archivos)
            {
                Console.WriteLine($"Detectado: {Path.GetFileName(archivo)}");

                try
                {
                    // A. Leemos el archivo
                    XDocument xml = XDocument.Load(archivo);

                    // B. Conectamos con la cola
                    using (MessageQueue cola = new MessageQueue(jav))
                    {
                        // C. Buscamos cada pago dentro del archivo
                        // (Asumimos que la etiqueta se llama 'Pago' o 'Payment', el código se adapta)
                        foreach (var nodo in xml.Descendants("Pago"))
                        {
                            Message mensaje = new Message();
                            mensaje.Label = "Pago XML";
                            mensaje.Body = nodo.ToString(); // Enviamos el texto del XML

                            // Enviar a la cola
                            // IMPORTANTE: Si creaste la cola como 'Transaccional', usa esto:
                            using (MessageQueueTransaction tx = new MessageQueueTransaction())
                            {
                                tx.Begin();
                                cola.Send(mensaje, tx);
                                tx.Commit();
                            }
                            // Si la cola NO es transaccional, borra el bloque 'using' anterior y usa solo: cola.Send(mensaje);

                            Console.WriteLine(" -> Enviado a la cola MSMQ");
                        }
                    }

                    // D. Mover a procesados (Para no leerlo dos veces)
                    string carpetaProcesados = Path.Combine(rutaCarpeta, "Procesados");
                    if (!Directory.Exists(carpetaProcesados)) Directory.CreateDirectory(carpetaProcesados);

                    string destino = Path.Combine(carpetaProcesados, Path.GetFileName(archivo));
                    if (File.Exists(destino)) File.Delete(destino); // Borrar si ya existe

                    File.Move(archivo, destino);
                    Console.WriteLine("Archivo procesado y guardado.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error leyendo archivo: " + ex.Message);
                }
            }
        }
    }
}