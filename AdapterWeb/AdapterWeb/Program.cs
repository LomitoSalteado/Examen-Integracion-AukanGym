using System;
using System.Net.Http;
using System.Messaging; // Para MSMQ
using System.Transactions; // Para TransactionScope


namespace AdapterWeb
{
    class Program
    {
        // Cola de destino
        static string rutaCola = @".\Private$\jav_web_pagos";
        // API del profe
        static string urlApi = "http://localhost:5000/api/payments";

        static void Main(string[] args)
        {
            Console.Title = "Adapter Web (Puerto 5000)";
            Console.WriteLine("--- ADAPTER WEB INICIADO ---");

            // 1. Verificar si la cola existe
            if (!MessageQueue.Exists(rutaCola))
            {
                MessageQueue.Create(rutaCola, true); // True = Transaccional
                Console.WriteLine($"[Sistema] Cola creada: {rutaCola}");
            }

            // 2. Ciclo infinito de lectura
            while (true)
            {
                try
                {
                    Console.WriteLine("Consultando API REST...");

                    using (HttpClient clienteHttp = new HttpClient())
                    {
                        // A. Descargar datos de la web
                        // Bloqueante: Espera hasta tener respuesta
                        var respuesta = clienteHttp.GetStringAsync(urlApi).Result;

                        Console.WriteLine($"[Web] Datos recibidos: {respuesta.Length} caracteres.");

                        // B. Enviar a MSMQ
                        using (MessageQueue cola = new MessageQueue(rutaCola))
                        using (TransactionScope tx = new TransactionScope())
                        {
                            Message mensaje = new Message();
                            mensaje.Label = "PagoWeb";
                            mensaje.Body = respuesta; // Mandamos el JSON crudo

                            cola.Send(mensaje, MessageQueueTransactionType.Automatic);
                            tx.Complete(); // Confirmar transacción

                            Console.WriteLine(" -> [MSMQ] Enviado a la cola correctamente.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] {ex.Message}");
                }

                // Esperar 10 segundos antes de volver a consultar (para no saturar)
                System.Threading.Thread.Sleep(10000);
            }
        }
    }
}