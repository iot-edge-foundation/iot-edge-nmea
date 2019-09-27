# iot-edge-nmea

This Azure IoT Edge module parses NMEA message from multiple ports and outputs them as events. This module is an IoT Edge implementation of this [nmea library](https://github.com/sandervandevelde/nmeaparser). 

## Introduction

This module parses incoming NMEA messages like GSV, GGA, GSA, GLL, GMC, VTG and TXT. Incoming NMEA messages which are splitted up in multiple messages like GSA and GSV are combined.

Messages from multiple ports are *not* combined but splitted in multiple outputs.


This module is available in [Docker Hub](https://cloud.docker.com/repository/docker/svelde/iot-edge-nmea).

Use it in your IoT device with tag:

```
svelde/iot-edge-nmea:1.0.0-amd64
```

## Input

Messages are received on input 'input1'.

This is the Input format:

``` 
{
    [JsonProperty("data")]
    public string Data { get; set; }

    [JsonProperty("port")]
    public string Port { get; set; }

    [JsonProperty("timestampUtc")]
    public DateTime TimestampUtc { get; set; }
}
```

## Outputs

### Regular output

Parsed messages are send over an output with the same name as the port which submitted the NMEA messages.

So the parsed version of a NMEA message coming from 'ttyS0' will be available on output 'ttyS0'.

The Output format is depending on the specific parsed message. See [the supported NMEA parser classes](https://github.com/sandervandevelde/nmeaparser/tree/master/src/svelde.nmea.parser) for the specific messages.  

### Exceptional output

In case a message can not be parsed or another excpetion occurs, a message will be put on output 'Exception'.

This is the Exception format:

```
public class ExceptionMessage
{
    [JsonProperty("message")]
    public string Message { get; set; }
}
```

## Routes



{
  "routes": {
    "serialToNmea": "FROM /messages/modules/serial/outputs/ttyACM0 INTO BrokeredEndpoint(\"/modules/nmea/inputs/input1\")",
    "nmeaToEcho": "FROM /messages/modules/nmea/outputs/Exception INTO BrokeredEndpoint(\"/modules/echo/inputs/input1\")",
    "route": "FROM /messages/modules/nmea/outputs/ttyACM0 INTO $upstream"
  }
}
