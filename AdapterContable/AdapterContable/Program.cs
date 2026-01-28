using System;
using System.Messaging; 
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace AdapterContable
{
    class Program
    {
        static string colaEntrada = @".\Private$\jav_datatype_pagos";
        static string colaSalida = @".\Private$\jav_account_status";

        static void Main(string[] args)
        {
            Console.WriteLine("--- ADAPTER CONTABLE (SOAP) ---");

            if (!MessageQueue.Exists(colaSalida)) MessageQueue.Create(colaSalida, true);

            if (!MessageQueue.Exists(colaEntrada))
            {
                Console.WriteLine("ERROR: No existe la cola de entrada. Ejecuta el Traductor.");
                Console.ReadLine();
                return;
            }

            using (MessageQueue cola = new MessageQueue(colaEntrada))
            {
                cola.Formatter = new XmlMessageFormatter(new String[] { "System.String,mscorlib" });
                Console.WriteLine("Esperando pagos...");

                while (true)
                {
                    try
                    {
                        // CORRECCIÓN 1: Especificamos que es un mensaje de Sistema de Colas
                        System.Messaging.Message mensaje = cola.Receive();
                        string json = mensaje.Body.ToString();

                        Console.WriteLine(" -> Procesando...");
                        ProcesarConSOAP(json);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error esperando: " + ex.Message);
                    }
                }
            }
        }

        static void ProcesarConSOAP(string json)
        {
            try
            {
                JObject datos = JObject.Parse(json);
                string rut = datos["rut_cliente"].ToString();
                int monto = int.Parse(datos["monto"].ToString());

                // Llamada al SOAP
                var cliente = new ServiceContable.ContabilidadServiceClient();
                var respuesta = cliente.RegistrarPago(rut, monto);

                // CORRECCIÓN 2: Usamos Mayúsculas (Saldo y ClienteId)
                Console.WriteLine($" [SOAP] Cliente: {respuesta.ClienteId} | Saldo: {respuesta.Saldo}");

                var resultadoJson = new
                {
                    rut = respuesta.ClienteId, // Mayúscula aquí también
                    saldo = respuesta.Saldo,   // Mayúscula aquí también
                    estado = (respuesta.Saldo <= 0) ? "AL DIA" : "DEUDOR"
                };

                EnviarRespuesta(JsonConvert.SerializeObject(resultadoJson));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error SOAP: " + ex.Message);
            }
        }

        static void EnviarRespuesta(string json)
        {
            using (MessageQueue cola = new MessageQueue(colaSalida))
            {
                System.Messaging.Message msg = new System.Messaging.Message();
                msg.Label = "Estado Cuenta";
                msg.Body = json;

                using (MessageQueueTransaction tx = new MessageQueueTransaction())
                {
                    tx.Begin();
                    cola.Send(msg, tx);
                    tx.Commit();
                }
                Console.WriteLine(" -> Resultado guardado.");
            }
        }
    }
}