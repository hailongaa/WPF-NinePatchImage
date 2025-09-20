
## 三方库配置：
``` text
SkiaSharp 3.119.0
SkiaSharp Views  3.119.0
```
## 使用方法：
### 1、 c#后台定义
``` c#
    NinePatchImage ninePatchImage = new NinePatchImage();
    ninePatchImage.Source = new BitmapImage(new Uri("Your.9.png"));
```
### 2、wpf直接写
``` xaml
    <local:NinePatchImage x:Name="gpuImage"  HorizontalAlignment="Center" VerticalAlignment="Center" Source="Your.9.png"/>
```
