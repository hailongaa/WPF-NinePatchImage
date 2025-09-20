
三方库配置：
SkiaSharp 3.119.0
SkiaSharp Views  3.119.0
使用方法：
1、 c#后台定义   
    NinePatchImage ninePatchImage = new NinePatchImage();
    ninePatchImage.Source = new BitmapImage(new Uri("Your.9.png"));
2、wpf直接写
    <local:NinePatchImage x:Name="gpuImage"  HorizontalAlignment="Center" VerticalAlignment="Center" Source="Your.9.png"/>
