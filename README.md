# Examen de Integración de Sistemas - Caso AukanGym

 El proyecto orquesta la ingesta de pagos desde múltiples canales, su validación contable y su envío final a un sistema de agendamiento Java.

## Descripción de la Arquitectura
El sistema utiliza el patrón Pipes and Filters para procesar la información de manera desacoplada:

1. Ingesta: Adaptadores que escuchan APIs REST y Archivos XML.
2. Mensajería Local: Uso de MSMQ (Microsoft Message Queuing) para transporte transaccional en Windows.
3. Lógica de Negocio: Normalización de datos (JSON Canónico) y validación de deuda con sistema Legacy (SOAP).
4. Interoperabilidad: Un Bridge que conecta .NET con Java mediante protocolo OpenWire.

## Componentes del Proyecto

* AdapterWeb: Cliente HTTP que consume la API REST (Puerto 5000).
* AdapterXML: File Watcher para procesamiento de archivos por lotes.
* TranslatorService: Normalizador de mensajes a formato estándar.
* AdapterContable: Cliente SOAP que valida saldos en el Sistema Legacy (Puerto 5001).
* BridgeService: Servicio de salida que transfiere mensajes de MSMQ a ActiveMQ.

## Tecnologías Utilizadas

* Lenguaje: C# (.NET Framework)
* Middleware Windows: MSMQ (Colas Privadas Transaccionales)
* Middleware Destino: Apache ActiveMQ (Classic)
* Formatos: XML, JSON, SOAP
* Librerías: Apache.NMS, Newtonsoft.Json, System.Messaging

## Requisitos de Ejecución

1. Visual Studio 2019/2022.
2. Tener activado MSMQ en las características de Windows.
3. Tener una instancia de ActiveMQ corriendo (Puerto 61616).
4. Ejecutar los simuladores WebPagos.exe y ContabilidadService.

---
Alumno: Joaquín
Asignatura: Integración de Sistemas
