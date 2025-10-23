using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestBookletProcessor.Core.Interfaces;

public interface IImageProcessor
{
    Task DeskewImageAsync(string imagePath, string outputPath);
    Task AlignImageAsync(string imagePath, string templatePath, string outputPath);
    Task<Double> DetectSkewAngleAsync(string imagePath);
}