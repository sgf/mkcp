using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class ConvManager
{
    static uint current_conv = 0;
    /// <summary>
    /// 生成通讯的唯一识别码
    /// </summary>
    /// <returns></returns>
    public static uint GenerateConv()
    {
        current_conv++;
        return current_conv;
    }
}

