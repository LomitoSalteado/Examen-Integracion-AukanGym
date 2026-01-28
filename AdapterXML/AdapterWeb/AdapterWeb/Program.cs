using System;
using System.Net.Http;
using System.Messaging;
using Newtonsoft.Json.Linq; 
using System.Threading;
using System.Threading.Tasks;

namespace AdapterWeb
{
    class Program
    {
        // CONFIGURACIÓN (Según README y  colas)
        static string nombreCola = @".\Private$\jav_web_pagos";
        static string urlProfesor = "http://localhost:5000/api/payments/today"; // Endpoint del README

        static async Task Main(string[] args)
        {
            Console.WriteLine("--- ADAPTER WEB INICIADO ---");
            Console.WriteLine($"1. Conectando al simulador: {urlProfesor}");
            Console.WriteLine($"2. Enviando a cola: {nombreCola}");

            // Verificamos si la cola existe
            if (!MessageQueue.Exists(nombreCola))
            {
                Console.WriteLine($"[ERROR] La cola {nombreCola} no existe.");
                Console.WriteLine("Por favor créala en Windows (Transaccional) antes de seguir.");
                Console.ReadLine();
                return;
            }

            while (true)
            {
                try
                {
                    await ConsultarSistemaDelProfe();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ERROR CONEXIÓN] ¿Está ejecutándose WebPagos.exe?");
                    Console.WriteLine("Detalle: " + ex.Message);
                }

                Console.WriteLine("Esperando 10 segundos para la próxima consulta...");
                Thread.Sleep(10000); // Esperamos 10 segundos
            }
        }

        static async Task ConsultarSistemaDelProfe()
        {
            using (HttpClient cliente = new HttpClient())
            {
                // A. Le pedimos los datos al EXE del profesor
                string respuestaJson = await cliente.GetStringAsync(urlProfesor);

                // Si llegamos aquí, hubo conexión!
                Console.WriteLine(" -> Datos recibidos del Sistema Web.");

                JArray pagos = JArray.Parse(respuestaJson);

                if (pagos.Count == 0)
                {
                    Console.WriteLine(" -> El sistema dice que no hay pagos hoy.");
                    return;
                }

                // B. Enviamos cada pago a la cola
                using (MessageQueue cola = new MessageQueue(nombreCola))
                {
                    foreach (var pago in pagos)
                    {
                        Message mensaje = new Message();
                        mensaje.Label = "Pago Web JSON";
                        mensaje.Body = pago.ToString();

                        // Usamos transacción
                        using (MessageQueueTransaction tx = new MessageQueueTransaction())
                        {
                            tx.Begin();
                            cola.Send(mensaje, tx);
                            tx.Commit();
                        }
                        Console.WriteLine(" -> [EXITO] Pago enviado a MSMQ.");
                    }
                }
            }
        }
    }
}