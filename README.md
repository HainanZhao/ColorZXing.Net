# ColorZXing.Net
A colorful QR code generator based on ZXing.Net

# Features
* Generate and Decode QR Code with Mono tone Color. 
```
//To generate a QR code with Red and Pink color instead of Black and White
var bitmap = ColorZXingMono.Encode("This is the text you want to encode", 400, 400, 0, Color.Red, Color.Pink);

//To decode the same QR code
var txtDecoded = ColorZXingMono.Decode(bitmapRead)
```
* Generate and Decode QR Code fully utilizing the RGB channels. You can encode more information with this method.
```
//To generate a colorful QR Code with RGB colors
var bitmap = ColorZXingRGB.Encode("This is the text you want to encode", 400, 400, 0);

//To decode the same QR Code
var txtDecoded = ColorZXingRGB.Decode(bitmap);
```
