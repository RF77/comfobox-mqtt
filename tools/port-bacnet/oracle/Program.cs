// Oracle: erzeugt die Golden-Frames aus dem LEGACY-Codec (src_old/Bacnet/BACnetBase.cs).
// Wird nur zur Erzeugung der Fixture verwendet - nicht Teil des Repositorys.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.BACnet;
using System.IO.BACnet.Serialize;
using System.Text;

internal static class Program
{
    private static readonly List<string> Names = new();
    private static readonly List<string> Kinds = new();
    private static readonly List<string> Raws = new();
    private static readonly List<string> Expects = new();

    private static void Add(string name, string kind, byte[] raw, string expect)
    {
        Names.Add(name);
        Kinds.Add(kind);
        Raws.Add(ToHex(raw));
        Expects.Add(expect);
    }

    private static string ToHex(byte[] data)
    {
        var sb = new StringBuilder();
        foreach (var b in data)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static byte[] Take(EncodeBuffer buffer)
    {
        var all = buffer.ToArray();
        var result = new byte[buffer.offset];
        Array.Copy(all, result, buffer.offset);
        return result;
    }

    private static string J(string s) => System.Text.Json.JsonSerializer.Serialize(s);

    private static int Main(string[] args)
    {
        var device = new BacnetAddress(BacnetAddressTypes.MSTP, 0, new byte[] { 1, 2, 3, 4, 5, 6 });

        // --- 1. I-Am Broadcast (Service) -------------------------------------
        {
            var buffer = new EncodeBuffer();
            Services.EncodeIamBroadcast(buffer, 1, 480, BacnetSegmentations.SEGMENTATION_BOTH, 0);
            Services.DecodeIamBroadcast(buffer.buffer, 0, out uint deviceId, out uint maxApdu,
                out BacnetSegmentations segmentation, out ushort vendorId);
            Add("IamBroadcast", "iam", Take(buffer),
                $"{{\"deviceId\":{deviceId},\"maxApdu\":{maxApdu},\"segmentation\":{J(segmentation.ToString())},\"vendorId\":{vendorId}}}");
        }

        // --- 2. Who-Is Broadcast (Service) -----------------------------------
        {
            var buffer = new EncodeBuffer();
            Services.EncodeWhoIsBroadcast(buffer, 0, 4194303);
            Services.DecodeWhoIsBroadcast(buffer.buffer, 0, buffer.offset, out int low, out int high);
            Add("WhoIsBroadcast", "whois", Take(buffer), $"{{\"lowLimit\":{low},\"highLimit\":{high}}}");
        }

        // --- 3. ReadProperty (Service) --------------------------------------
        {
            var buffer = new EncodeBuffer();
            var objectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62);
            Services.EncodeReadProperty(buffer, objectId, 85);
            Services.DecodeReadProperty(buffer.buffer, 0, buffer.offset, out BacnetObjectId decoded,
                out BacnetPropertyReference property);
            Add("ReadProperty", "readProperty", Take(buffer),
                $"{{\"objectType\":{J(decoded.type.ToString())},\"instance\":{decoded.instance},\"propertyId\":{property.propertyIdentifier},\"arrayIndex\":{property.propertyArrayIndex}}}");
        }

        // --- 4. WriteProperty (Service) -------------------------------------
        {
            var buffer = new EncodeBuffer();
            var objectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62);
            var values = new List<BacnetValue> { new BacnetValue(21.5f) };
            Services.EncodeWriteProperty(buffer, objectId, 85, ASN1.BACNET_ARRAY_ALL, 16, values);
            Services.DecodeWriteProperty(buffer.buffer, 0, buffer.offset, out BacnetObjectId decoded,
                out BacnetPropertyValue value);
            Add("WriteProperty", "writeProperty", Take(buffer),
                $"{{\"objectType\":{J(decoded.type.ToString())},\"instance\":{decoded.instance},\"propertyId\":{value.property.propertyIdentifier},\"priority\":{value.priority},\"value\":{J(Convert.ToString(value.value[0].Value, CultureInfo.InvariantCulture))}}}");
        }

        // --- 5. ReadPropertyAcknowledge (Service) ---------------------------
        {
            var buffer = new EncodeBuffer();
            var objectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62);
            var values = new List<BacnetValue> { new BacnetValue(21.5f) };
            Services.EncodeReadPropertyAcknowledge(buffer, objectId, 85, ASN1.BACNET_ARRAY_ALL, values);
            Services.DecodeReadPropertyAcknowledge(buffer.buffer, 0, buffer.offset, out BacnetObjectId decoded,
                out BacnetPropertyReference property, out IList<BacnetValue> decodedValues);
            Add("ReadPropertyAcknowledge", "readPropertyAcknowledge", Take(buffer),
                $"{{\"objectType\":{J(decoded.type.ToString())},\"instance\":{decoded.instance},\"propertyId\":{property.propertyIdentifier},\"arrayIndex\":{property.propertyArrayIndex},\"value\":{J(Convert.ToString(decodedValues[0].Value, CultureInfo.InvariantCulture))}}}");
        }

        // --- 6. Error (Service) --------------------------------------------
        {
            var buffer = new EncodeBuffer();
            Services.EncodeError(buffer, BacnetErrorClasses.ERROR_CLASS_PROPERTY,
                BacnetErrorCodes.ERROR_CODE_UNKNOWN_PROPERTY);
            Services.DecodeError(buffer.buffer, 0, buffer.offset, out BacnetErrorClasses errorClass,
                out BacnetErrorCodes errorCode);
            Add("Error", "error", Take(buffer),
                $"{{\"errorClass\":{J(errorClass.ToString())},\"errorCode\":{J(errorCode.ToString())}}}");
        }

        // --- 7. Confirmed Service Request (APDU) ----------------------------
        {
            var buffer = new EncodeBuffer();
            APDU.EncodeConfirmedServiceRequest(buffer, BacnetPduTypes.PDU_TYPE_CONFIRMED_SERVICE_REQUEST,
                BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, BacnetMaxSegments.MAX_SEG2,
                BacnetMaxAdpu.MAX_APDU480, 1, 0, 0);
            APDU.DecodeConfirmedServiceRequest(buffer.buffer, 0, out BacnetPduTypes type,
                out BacnetConfirmedServices service, out BacnetMaxSegments maxSegments, out BacnetMaxAdpu maxAdpu,
                out byte invokeId, out byte sequenceNumber, out byte proposedWindow);
            Add("ConfirmedServiceRequest", "confirmedRequest", Take(buffer),
                $"{{\"type\":{J(type.ToString())},\"service\":{J(service.ToString())},\"maxSegments\":{J(maxSegments.ToString())},\"maxApdu\":{J(maxAdpu.ToString())},\"invokeId\":{invokeId},\"sequenceNumber\":{sequenceNumber},\"proposedWindow\":{proposedWindow}}}");
        }

        // --- 8. Unconfirmed Service Request (APDU) --------------------------
        {
            var buffer = new EncodeBuffer();
            APDU.EncodeUnconfirmedServiceRequest(buffer, BacnetPduTypes.PDU_TYPE_UNCONFIRMED_SERVICE_REQUEST,
                BacnetUnconfirmedServices.SERVICE_UNCONFIRMED_I_AM);
            APDU.DecodeUnconfirmedServiceRequest(buffer.buffer, 0, out BacnetPduTypes type,
                out BacnetUnconfirmedServices service);
            Add("UnconfirmedServiceRequest", "unconfirmedRequest", Take(buffer),
                $"{{\"type\":{J(type.ToString())},\"service\":{J(service.ToString())}}}");
        }

        // --- 9. Simple Ack (APDU) -------------------------------------------
        {
            var buffer = new EncodeBuffer();
            APDU.EncodeSimpleAck(buffer, BacnetPduTypes.PDU_TYPE_SIMPLE_ACK,
                BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY, 7);
            APDU.DecodeSimpleAck(buffer.buffer, 0, out BacnetPduTypes type,
                out BacnetConfirmedServices service, out byte invokeId);
            Add("SimpleAck", "simpleAck", Take(buffer),
                $"{{\"type\":{J(type.ToString())},\"service\":{J(service.ToString())},\"invokeId\":{invokeId}}}");
        }

        // --- 10. Complex Ack (APDU) -----------------------------------------
        {
            var buffer = new EncodeBuffer();
            APDU.EncodeComplexAck(buffer, BacnetPduTypes.PDU_TYPE_COMPLEX_ACK,
                BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, 3, 0, 0);
            APDU.DecodeComplexAck(buffer.buffer, 0, out BacnetPduTypes type,
                out BacnetConfirmedServices service, out byte invokeId, out byte sequenceNumber,
                out byte proposedWindow);
            Add("ComplexAck", "complexAck", Take(buffer),
                $"{{\"type\":{J(type.ToString())},\"service\":{J(service.ToString())},\"invokeId\":{invokeId},\"sequenceNumber\":{sequenceNumber},\"proposedWindow\":{proposedWindow}}}");
        }

        // --- 11. Error (APDU) -----------------------------------------------
        {
            var buffer = new EncodeBuffer();
            APDU.EncodeError(buffer, BacnetPduTypes.PDU_TYPE_ERROR,
                BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY, 9);
            APDU.DecodeError(buffer.buffer, 0, out BacnetPduTypes type, out BacnetConfirmedServices service,
                out byte invokeId);
            Add("ApduError", "apduError", Take(buffer),
                $"{{\"type\":{J(type.ToString())},\"service\":{J(service.ToString())},\"invokeId\":{invokeId}}}");
        }

        // --- 12. Abort (APDU) ------------------------------------------------
        {
            var buffer = new EncodeBuffer();
            APDU.EncodeAbort(buffer, BacnetPduTypes.PDU_TYPE_ABORT, 4, 1);
            APDU.DecodeAbort(buffer.buffer, 0, out BacnetPduTypes type, out byte invokeId, out byte reason);
            Add("Abort", "abort", Take(buffer),
                $"{{\"type\":{J(type.ToString())},\"invokeId\":{invokeId},\"reason\":{reason}}}");
        }

        // --- 13. Segment Ack (APDU) ------------------------------------------
        {
            var buffer = new EncodeBuffer();
            APDU.EncodeSegmentAck(buffer, BacnetPduTypes.PDU_TYPE_SEGMENT_ACK, 2, 1, 4);
            APDU.DecodeSegmentAck(buffer.buffer, 0, out BacnetPduTypes type, out byte originalInvokeId,
                out byte sequenceNumber, out byte actualWindowSize);
            Add("SegmentAck", "segmentAck", Take(buffer),
                $"{{\"type\":{J(type.ToString())},\"originalInvokeId\":{originalInvokeId},\"sequenceNumber\":{sequenceNumber},\"actualWindowSize\":{actualWindowSize}}}");
        }

        // --- 14. NPDU mit Quelladresse --------------------------------------
        {
            var buffer = new EncodeBuffer();
            NPDU.Encode(buffer, BacnetNpduControls.ExpectingReply, device, null, 255,
                BacnetNetworkMessageTypes.NETWORK_MESSAGE_I_AM_ROUTER_TO_NETWORK, 0);
            NPDU.Decode(buffer.buffer, 0, out BacnetNpduControls function, out BacnetAddress destination,
                out BacnetAddress source, out byte hopCount, out BacnetNetworkMessageTypes networkMsgType,
                out ushort vendorId);
            Add("NpduExpectingReply", "npdu", Take(buffer),
                $"{{\"function\":{J(function.ToString())},\"source\":{J(source == null ? string.Empty : source.ToString())},\"hopCount\":{hopCount},\"networkMessageType\":{J(networkMsgType.ToString())}}}");
        }

        // --- 15. Adressen (ASN.1) -------------------------------------------
        {
            var buffer = new EncodeBuffer();
            device.ASN1encode(buffer);
            Add("AddressMac", "addressMac", Take(buffer),
                $"{{\"type\":{J(device.type.ToString())},\"net\":{device.net},\"adrLength\":{device.adr.Length},\"adr\":{J(ToHex(device.adr))}}}");
        }

        // --- 16. ReadProperty-Varianten ------------------------------------
        {
            var variants = new (BacnetObjectTypes type, uint instance, uint property, string label)[]
            {
                (BacnetObjectTypes.OBJECT_DEVICE, 1, 77, "ReadProperty Device/ObjectName"),
                (BacnetObjectTypes.OBJECT_ANALOG_VALUE, 72, 85, "ReadProperty Warmwater/PresentValue"),
                (BacnetObjectTypes.OBJECT_ANALOG_VALUE, 3442, 85, "ReadProperty Comfofond/PresentValue"),
            };
            foreach (var variant in variants)
            {
                var buffer = new EncodeBuffer();
                var objectId = new BacnetObjectId(variant.type, variant.instance);
                Services.EncodeReadProperty(buffer, objectId, variant.property);
                Services.DecodeReadProperty(buffer.buffer, 0, buffer.offset, out BacnetObjectId decoded,
                    out BacnetPropertyReference property);
                Add(variant.label, "readProperty", Take(buffer),
                    $"{{\"objectType\":{J(decoded.type.ToString())},\"instance\":{decoded.instance},\"propertyId\":{property.propertyIdentifier},\"arrayIndex\":{property.propertyArrayIndex}}}");
            }
        }

        // --- 17. WriteProperty-Varianten -----------------------------------
        {
            var variants = new (BacnetObjectTypes type, uint instance, uint property, float value, byte priority, string label)[]
            {
                (BacnetObjectTypes.OBJECT_ANALOG_VALUE, 72, 85, 50.0f, 8, "WriteProperty Warmwater"),
                (BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62, 85, 5.0f, 16, "WriteProperty Minimum"),
                (BacnetObjectTypes.OBJECT_ANALOG_VALUE, 62, 85, 30.0f, 1, "WriteProperty Maximum"),
            };
            foreach (var variant in variants)
            {
                var buffer = new EncodeBuffer();
                var objectId = new BacnetObjectId(variant.type, variant.instance);
                var values = new List<BacnetValue> { new BacnetValue(variant.value) };
                Services.EncodeWriteProperty(buffer, objectId, variant.property, ASN1.BACNET_ARRAY_ALL,
                    variant.priority, values);
                Services.DecodeWriteProperty(buffer.buffer, 0, buffer.offset, out BacnetObjectId decoded,
                    out BacnetPropertyValue value);
                Add(variant.label, "writeProperty", Take(buffer),
                    $"{{\"objectType\":{J(decoded.type.ToString())},\"instance\":{decoded.instance},\"propertyId\":{value.property.propertyIdentifier},\"priority\":{value.priority},\"value\":{J(Convert.ToString(value.value[0].Value, CultureInfo.InvariantCulture))}}}");
            }
        }

        // --- 18. I-Am mit maximalem APDU -----------------------------------
        {
            var buffer = new EncodeBuffer();
            Services.EncodeIamBroadcast(buffer, 1, 1476, BacnetSegmentations.SEGMENTATION_NONE, 42);
            Services.DecodeIamBroadcast(buffer.buffer, 0, out uint deviceId, out uint maxApdu,
                out BacnetSegmentations segmentation, out ushort vendorId);
            Add("IamBroadcastMaxApdu", "iam", Take(buffer),
                $"{{\"deviceId\":{deviceId},\"maxApdu\":{maxApdu},\"segmentation\":{J(segmentation.ToString())},\"vendorId\":{vendorId}}}");
        }

        // --- 19. Adressen: IP und DEVICE -----------------------------------
        {
            var ip = new BacnetAddress(BacnetAddressTypes.IP, 0, new byte[] { 192, 168, 1, 42 });
            var buffer = new EncodeBuffer();
            ip.ASN1encode(buffer);
            Add("AddressIp", "addressIp", Take(buffer),
                $"{{\"type\":{J(ip.type.ToString())},\"net\":{ip.net},\"adrLength\":{ip.adr.Length},\"adr\":{J(ToHex(ip.adr))}}}");

            var deviceAddress = new BacnetAddress(BacnetAddressTypes.None, 0, new byte[0]);
            var buffer2 = new EncodeBuffer();
            deviceAddress.ASN1encode(buffer2);
            Add("AddressDevice", "addressDevice", Take(buffer2),
                $"{{\"type\":{J(deviceAddress.type.ToString())},\"net\":{deviceAddress.net},\"adrLength\":{deviceAddress.adr.Length},\"adr\":{J(ToHex(deviceAddress.adr))}}}");
        }

        // --- 20. NPDU ohne Quelladresse ------------------------------------
        {
            var buffer = new EncodeBuffer();
            NPDU.Encode(buffer, BacnetNpduControls.PriorityNormalMessage, null, null, 0,
                BacnetNetworkMessageTypes.NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK, 0);
            NPDU.Decode(buffer.buffer, 0, out BacnetNpduControls function, out BacnetAddress destination,
                out BacnetAddress source, out byte hopCount, out BacnetNetworkMessageTypes networkMsgType,
                out ushort vendorId);
            Add("NpduPriorityNormal", "npdu", Take(buffer),
                $"{{\"function\":{J(function.ToString())},\"source\":{J(source == null ? string.Empty : source.ToString())},\"hopCount\":{hopCount},\"networkMessageType\":{J(networkMsgType.ToString())}}}");
        }

        // --- JSON schreiben --------------------------------------------------
        var sb = new StringBuilder();
        sb.Append("{\n  \"source\": \"src_old/Bacnet/BACnetBase.cs\",\n");
        sb.Append("  \"generatedBy\": \"Oracle über den Legacy-Codec (Phase 1, Aufgabe 1.1)\",\n");
        sb.Append("  \"frames\": [\n");
        for (var i = 0; i < Names.Count; i++)
        {
            sb.Append("    {\n");
            sb.Append($"      \"name\": {J(Names[i])},\n");
            sb.Append($"      \"kind\": {J(Kinds[i])},\n");
            sb.Append($"      \"raw\": {J(Raws[i])},\n");
            sb.Append($"      \"expect\": {Expects[i]}\n");
            sb.Append(i == Names.Count - 1 ? "    }\n" : "    },\n");
        }
        sb.Append("  ]\n}\n");
        File.WriteAllText(args.Length > 0 ? args[0] : "bacnet-frames.json", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"{Names.Count} Frames geschrieben.");
        return 0;
    }
}