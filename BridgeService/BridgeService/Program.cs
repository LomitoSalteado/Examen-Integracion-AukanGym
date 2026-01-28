using System;
using System.Messaging; // Para Windows MSMQ
using Apache.NMS;       // Para Java ActiveMQ
using Apache.NMS.Util;  // Utilidades
using System.Threading;

namespace BridgeService
{
    class Program
    {
        // 1. ORIGEN (Windows)
        static string colaOrigen = @".\Private$\jav_account_status";

        // 2. DESTINO (Java ActiveMQ)
        // El puerto 61616 es donde ActiveMQ escucha datos (el 8161 es solo para la web)
        static string brokerUri = "tcp://localhost:61616";
        static string colaDestinoJava = "ColaPagosJava";

        static void Main(string[] args)
        {
            Console.WriteLine("--- BRIDGE: WINDOWS -> JAVA ---");
            Console.WriteLine($"Leyendo de MSMQ: {colaOrigen}");
            Console.WriteLine($"Enviando a ActiveMQ: {colaDestinoJava}");

            // Conexión a MSMQ
            if (!MessageQueue.Exists(colaOrigen))
            {
                Console.WriteLine("ERROR: No existe la cola de origen en Windows.");
                Console.ReadLine();
                return;
            }

            // Preparar la conexión a Java (ActiveMQ)
            IConnectionFactory fabrica = new NMSConnectionFactory(brokerUri);

            using (IConnection conexionJava = fabrica.CreateConnection())
            using (ISession sesionJava = conexionJava.CreateSession())
            {
                // Abrimos la conexión con Java
                conexionJava.Start();
                IDestination destino = sesionJava.GetQueue(colaDestinoJava);
                IMessageProducer productor = sesionJava.CreateProducer(destino);

                // Modo "Persistente" (para que no se pierdan los mensajes si se apaga Java)
                productor.DeliveryMode = MsgDeliveryMode.Persistent;

                // Conexión a Windows (MSMQ)
                using (MessageQueue colaWindows = new MessageQueue(colaOrigen))
                {
                    colaWindows.Formatter = new XmlMessageFormatter(new String[] { "System.String,mscorlib" });

                    Console.WriteLine("Puente activo. Esperando mensajes...");

                    while (true)
                    {
                        try
                        {
                            // A. Sacar de Windows
                            // Usamos transacción para no perderlo si falla el puente
                            Message mensajeWin = colaWindows.Receive();
                            string jsonCuerpo = mensajeWin.Body.ToString();

                            Console.WriteLine(" -> [MSMQ] Recibido. Cruzando frontera...");

                            // B. Meter en Java
                            ITextMessage mensajeJava = sesionJava.CreateTextMessage(jsonCuerpo);
                            productor.Send(mensajeJava);

                            Console.WriteLine(" -> [JAVA] ¡Enviado a ActiveMQ exitosamente!");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error en el puente: " + ex.Message);
                            Thread.Sleep(2000); // Esperar si hay error
                        }
                    }
                }
            }
        }
    }
}