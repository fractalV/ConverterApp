using System;
using System.Collections.Generic;
using System.Text;

namespace ConverterApp
{
    //описание соответвия полей RU=LT
    class DictionaryClass
    {
        readonly Dictionary<string, string> fields = new Dictionary<string, string>
        {
            //HEAHEA Заголовок
            {"00000000", "RefNumEPT1"}, //код таможни вывоза
            {"XXXXX", "TypOfOpeHEA994"}, //тип операции
            {"1", "CurDocIndHEA986"},  //копия документа актуальна (1/0)
            //GOOITEGDS товар
            {"ESADout_CUGoods#catESAD_cu:GoodsNumeric","IteNumB32F1"}, //номер товара
            {"ESADout_CUGoods#catESAD_cu:GoodsTNVEDCode","ComCodTarCodGDS10" },  //код ТН ВЭД
            {"ESADout_CUGoods#catESAD_cu:GrossWeightQuantity", "GroMasGDS46" },  //масса брутто
            {"ESADout_CUGoods#catESAD_cu:NetWeightQuantity", "NetMasGDS48" },  //масса нетто
            {"ESADout_CUGoods#catESAD_cu:InvoicedCost","GooInvB42" }
        };
    }
}
