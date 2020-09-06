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
![](Images/redpink.png)
* Generate and Decode QR Code fully utilizing the RGB channels. In theory, you can encode 3x more information with this method, compared to the black and white only method.
```
//To generate a colorful QR Code with RGB colors
var bitmap = ColorZXingRGB.Encode("This is the text you want to encode", 400, 400, 0);

//To decode the same QR Code
var txtDecoded = ColorZXingRGB.Decode(bitmap);
```
![](Images/test.png)


As a comparasion, this is the same QR images generated using only Black and White color.
![](Images/basic.png)
