using ConverterApp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;


namespace ConverterApp
{
    class Program
    {
        private static readonly DateTime dateresult;
        private static bool bConsigneeEqual;
        private static string inputfile;
        private static string key_API_Translator;
        private static bool bEsadout;       

        static void Main(string[] args)
        {
       
            DirectoryInfo dirOutput;

            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string displayableVersion = $"{version}";

            Console.WriteLine("Программма для конвертирования файлов ЭДТ XML или EMDAS XML");
            Console.WriteLine($"Версия [{displayableVersion}]");
            Console.WriteLine("Текущая версия формата EMDAS.LTT.04.PIEL.01.03.U.2018");
            Console.WriteLine("(c) Альта-Софт (Alta-Soft), 2019.");
            Console.WriteLine();

            if (!String.IsNullOrWhiteSpace(args.ToString()))
            {
                string path = "";
                foreach (string arg in args)
                {
                    if (arg == "/?")
                    {
                        Console.WriteLine("converterapp.exe <file>.xml [/out:] [/t]");
                        Console.WriteLine();
                        Console.WriteLine("/out:<directoryName>");
                        Console.WriteLine("    Выходной каталог, в котором создаются файлы. По умолчанию");
                        Console.WriteLine("    используется текущий каталог. Краткая форма: '/o:'.");
                        Console.WriteLine("/t:<key>");
                        Console.WriteLine("    Включить перевод сервисом «Яндекс.Переводчик»");
                        Console.WriteLine("    http://translate.yandex.ru/");
                        Console.WriteLine("    key - ключ API. Ключ получить можно тут https://translate.yandex.ru/developers/keys");
                    }
                    else
                    if (arg.StartsWith("/o:") || arg.StartsWith("/out:"))
                    {
                        if (arg.StartsWith("/o:")) path = arg.Substring(2);
                        if (arg.StartsWith("/out:")) path = arg.Substring(4);
                        dirOutput = new DirectoryInfo(path);
                        if (!dirOutput.Exists)
                        {
                            dirOutput.Create();
                        }
                    }
                    if (arg.StartsWith("/t:"))
                    {
                        if (arg.StartsWith("/t:")) key_API_Translator = arg.Substring(2);
                    }
                    else inputfile = arg;
                }
            }

            //определим какой файл и куда коныертируем
            try {
                using (StreamReader sr = new StreamReader(inputfile))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        //Console.WriteLine(line);
                        if (line.Contains("ESADout_CU")) { bEsadout = true; break; } else bEsadout = false;
                    }
                }
            }

            catch (Exception e) {
                Console.WriteLine("Файл не может быть прочтён:");
                Console.WriteLine(e.Message);
            }

            var outObj = new Object();

            //bEsadout = true;

            if (bEsadout)    
                outObj = ConvertToEMDAS(inputfile);            
            else 
                outObj = ConvertToESAD(inputfile);
            
            
            XmlSerializer x = new System.Xml.Serialization.XmlSerializer(outObj.GetType());

                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.NewLineOnAttributes = true;
                XmlWriter writer = XmlWriter.Create("Root.xml", settings);

                using (XmlWriter xmlWriter = writer)
                {
                    x.Serialize(xmlWriter, outObj);
                }

                string str;
                str = File.ReadAllText("Root.xml");
                Console.WriteLine(str);

                Console.ReadKey();
            }
        //парсер в латвийский формат
        private static object ConvertToEMDAS(string inputfile)
        {
            CultureInfo culture = new CultureInfo("lv-LV");
            #region HEADER

            var outObj = new IcsIEType
            {
                MesTypMES20 = "XXX"
            };

            //создаем заголовок
            outObj.HEAHEA = new HEAHEAType
            {
                //обязательные поля
                //код таможни ввоза
                RefNumEPT1 = "00000000",
                //тип операции
                TypOfOpeHEA994 = "XXXXX",
                //признак актуальности документа (1-да/0-нет)
                CurDocIndHEA986 = HEAHEATypeTesIndMES18.Item1
            };

            //создаем контейнер LV xml           

            XNamespace awLV = "http://www.w3.org/2001/XMLSchema-instance";
            XElement rootLV = new XElement("IcsIE",
                new XAttribute(XNamespace.Xmlns + "xsi", awLV),
                new XElement("MesTypMES20", outObj.MesTypMES20),
                //заголовок
                new XElement("HEAHEA",
                    //добавляем три обязательных поля заголовка
                    new XElement("RefNumEPT1", outObj.HEAHEA.RefNumEPT1),
                    new XElement("TypOfOpeHEA994", outObj.HEAHEA.TypOfOpeHEA994),
                    new XElement("CurDocIndHEA986", outObj.HEAHEA.CurDocIndHEA986)
                    )
                );
            #endregion
            //десереализуем входной esadout
            ED_ContainerType MyObj;

            if (!String.IsNullOrEmpty(inputfile)) MyObj = DeserializeXMLFileToObject<ED_ContainerType>(inputfile);
                else MyObj = DeserializeXMLFileToObject<ED_ContainerType>("test.xml");

            var esadout = MyObj.ContainerDoc[0].DocBody;

            XmlNode root = esadout;

            ///<summary>
            ///Лист латвийскийх товаров
            ///</summary>
            var listOfGoods = new List<GOOITEGDSType>();
            ///<summary>
            ///Документы
            /// </summary>
            var listOfDocs = new List<PRODOCDC2Type>();
            //Упаковка
            var listOfPacs = new List<PACGS2Type>();
            //Контейнера
            var listofConts = new List<CONNR2Type>();
            //Отправитель
            TRACONCO1Type consignor = new TRACONCO1Type();
            //Получатель
            TRACONCE1Type consignee = new TRACONCE1Type();
            bConsigneeEqual = false; //не равен декларанту
                                     //декларант
            TRAREPType declarant = new TRAREPType();
            //54графа
            NOTPAR670Type fiiledperson = new NOTPAR670Type();

            if (root.HasChildNodes)
            {
                foreach (XmlElement element in root.ChildNodes)
                {
                    //54я графа
                    if (element.LocalName == "FilledPerson")
                    {
                        foreach (XmlElement xmlFilledPerson in element)
                        {
                            if (xmlFilledPerson.LocalName == "SigningDetails")
                            {
                                foreach (XmlElement xmlSigning in xmlFilledPerson)
                                {
                                    if (xmlSigning.LocalName == "PersonSurname") fiiledperson.NamNOTPAR672 = Translator.Translit(xmlSigning.InnerText);
                                    if (xmlSigning.LocalName == "PersonName") fiiledperson.NamNOTPAR672 = fiiledperson.NamNOTPAR672 + " " + Translator.Translit(xmlSigning.InnerText);
                                    if (xmlSigning.LocalName == "PersonMiddleName") fiiledperson.NamNOTPAR672 = fiiledperson.NamNOTPAR672 + " " + Translator.Translit(xmlSigning.InnerText);
                                }
                            }
                        }
                    };
                    //партия 
                    if (element.LocalName == "ESADout_CUGoodsShipment")
                    {
                        foreach (XmlElement elementCUGoodsShipment in element)
                        {
                            //Отправитель
                            if (elementCUGoodsShipment.LocalName == "ESADout_CUConsignor")
                            {
                                foreach (XmlElement xmlConsignor in elementCUGoodsShipment)
                                {
                                    if (xmlConsignor.LocalName == "OrganizationName")
                                    {
                                        consignor.NamCO17 = xmlConsignor.InnerXml;
                                        consignor.TINCO159 = " "; //If the Trader has a valid EORI Trader Identification number (TIN), then the TIN shall be declared. 
                                    }
                                    if (xmlConsignor.LocalName == "SubjectAddressDetails")
                                    {
                                        foreach (XmlElement xmlAdr in xmlConsignor)
                                        {
                                            //TODO: преобразовтаь код страны в формат EC
                                            if (xmlAdr.LocalName == "CountryCode") consignor.CouCO125 = xmlAdr.InnerXml;
                                            if (xmlAdr.LocalName == "City") consignor.CitCO124 = xmlAdr.InnerXml;
                                            if (xmlAdr.LocalName == "StreetHouse") consignor.StrAndNumCO122 = xmlAdr.InnerXml;
                                            if (xmlAdr.LocalName == "PostalCode") consignor.PosCodCO123 = xmlAdr.InnerXml;
                                        }
                                    }
                                }
                            };
                            //Получатель
                            if (elementCUGoodsShipment.LocalName == "ESADout_CUConsignee")
                            {
                                foreach (XmlElement xmlConsignee in elementCUGoodsShipment)
                                {
                                    if (xmlConsignee.LocalName == "OrganizationName")
                                    {
                                        consignee.NamCE17 = xmlConsignee.InnerXml;
                                        consignee.TINCE159 = " "; //If the Trader has a valid EORI Trader Identification number (TIN), then the TIN shall be declared. 
                                    };
                                    if (xmlConsignee.LocalName == "SubjectAddressDetails")
                                    {
                                        foreach (XmlElement xmlAdr in xmlConsignee)
                                        {
                                            //TODO: преобразовтаь код страны в формат EC
                                            if (xmlAdr.LocalName == "CountryCode") consignee.CouCE125 = xmlAdr.InnerXml;
                                            if (xmlAdr.LocalName == "City") consignee.CitCE124 = xmlAdr.InnerXml;
                                            if (xmlAdr.LocalName == "StreetHouse") consignee.StrAndNumCE122 = xmlAdr.InnerXml;
                                            if (xmlAdr.LocalName == "PostalCode") consignee.PosCodCE123 = xmlAdr.InnerXml;
                                        }
                                    }
                                    if (xmlConsignee.LocalName == "EqualIndicator") if (xmlConsignee.InnerXml == "1") bConsigneeEqual = true;
                                }
                            };

                            //Декларант
                            if (elementCUGoodsShipment.LocalName == "ESADout_CUDeclarant")
                            {
                                foreach (XmlElement xmlDeclarant in elementCUGoodsShipment)
                                {
                                    if (xmlDeclarant.LocalName == "OrganizationName")
                                    {
                                        declarant.NamTRE1 = xmlDeclarant.InnerXml;
                                        declarant.TINTRE1 = " ";
                                    }

                                    if (xmlDeclarant.LocalName == "SubjectAddressDetails")
                                    {
                                        foreach (XmlElement xmlAdr in xmlDeclarant)
                                        {
                                            //TODO: преобразовтаь код страны в формат EC
                                            if (xmlAdr.LocalName == "CountryCode") declarant.CouCodTRE1 = xmlAdr.InnerXml;
                                            if (xmlAdr.LocalName == "City") declarant.CitTRE1 = xmlAdr.InnerXml;
                                            if (xmlAdr.LocalName == "StreetHouse") declarant.StrAndNumTRE1 = xmlAdr.InnerXml;
                                            if (xmlAdr.LocalName == "PostalCode") declarant.PosCodTRE1 = xmlAdr.InnerXml;
                                        }
                                    }
                                }
                            };


                            if (elementCUGoodsShipment.LocalName == "ESADout_CUGoods")
                            {
                                ///<summary>
                                ///Латвиский товар
                                ///</summary>            
                                GOOITEGDSType tovarLV = new GOOITEGDSType();
                                foreach (XmlElement elemenGoods in elementCUGoodsShipment)
                                {
                                    decimal number;
                                    //разбор на элементы ы товаре(вынести в метод)
                                    switch (elemenGoods.LocalName)
                                    {
                                        //обязательные поля товара
                                        #region requiredFields
                                        case "GoodsNumeric":
                                            tovarLV.IteNumB32F1 = elemenGoods.InnerXml;
                                            // Console.WriteLine(elemenGoods.InnerXml);
                                            break;
                                        case "GoodsTNVEDCode":
                                            //код ТНВЭД
                                            tovarLV.ComCodTarCodGDS10 = elemenGoods.InnerXml;
                                            break;
                                        case "GrossWeightQuantity":
                                            //масса брутто
                                            if (Decimal.TryParse(elemenGoods.InnerXml, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number))
                                            {
                                                tovarLV.GroMasGDS46 = number;
                                            }
                                            else
                                                ErrorDtcimalConvertionNewMethod(elemenGoods);
                                            break;
                                        case "NetWeightQuantity":
                                            //масса нетто
                                            if (Decimal.TryParse(elemenGoods.InnerXml, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number))
                                            {
                                                tovarLV.NetMasGDS48 = number;
                                            }
                                            else
                                                ErrorDtcimalConvertionNewMethod(elemenGoods);
                                            break;
                                        case "InvoicedCost":
                                            //цена товара
                                            if (Decimal.TryParse(elemenGoods.InnerXml, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number))
                                            {
                                                tovarLV.GooInvB42 = number;
                                            }
                                            else
                                                ErrorDtcimalConvertionNewMethod(elemenGoods);
                                            break;
                                        //конец обязательных полей
                                        #endregion
                                        case "GoodsDescription":
                                            //наименование товара, не более 1000 символов
                                            tovarLV.GooDesGDS23 = tovarLV.GooDesGDS23 + elemenGoods.InnerXml;
                                            break;
                                        case "OriginCountryCode":
                                            //Код страны происхождения
                                            tovarLV.CouOfOriCodGDS63 = elemenGoods.InnerXml;
                                            break;
                                        case "SupplementaryGoodsQuantity":
                                            //доп.ед
                                            foreach (XmlElement xmlSupp in elemenGoods)
                                            {
                                                if (xmlSupp.LocalName == "GoodsQuantity")
                                                {
                                                    //количество
                                                    decimal num;
                                                    if (Decimal.TryParse(xmlSupp.InnerXml, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out num))
                                                    {
                                                        string tmp = num.ToString("N3", CultureInfo.InvariantCulture);
                                                        Decimal.TryParse(tmp, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
                                                        tovarLV.QuaOfGooGDS376 = number;
                                                    }
                                                    else Console.WriteLine($"Не могу конвертировать {0} тип Decimal (8.3)", xmlSupp.InnerText);
                                                }
                                                //код
                                                if (xmlSupp.LocalName == "MeasureUnitQualifierCode")
                                                {
                                                    if (tovarLV.QuaOfGooGDS376 != 0) tovarLV.BasUniOfMea377 = xmlSupp.InnerText;
                                                }
                                                //                                                    
                                                if (tovarLV.QuaOfGooGDS376 != 0 && tovarLV.BasUniOfMea377 != null) tovarLV.QuaOfGooGDS376Specified = true;
                                            }
                                            break;
                                        case "ESADout_CUPresentedDocument":
                                            //документы
                                            PRODOCDC2Type docs = new PRODOCDC2Type();
                                            foreach (XmlElement xmlPdoc in elemenGoods)
                                            {
                                                //номер
                                                if (xmlPdoc.LocalName == "PrDocumentNumber") docs.TitDC29 = xmlPdoc.InnerXml;
                                                //дата
                                                if (xmlPdoc.LocalName == "PrDocumentDate")
                                                {

                                                    if (DateTime.TryParseExact(xmlPdoc.InnerXml, "yyyy-MM-dd", culture,
                                                            System.Globalization.DateTimeStyles.AllowWhiteSpaces |
                                                            System.Globalization.DateTimeStyles.None,
                                                            out DateTime parsedDate))
                                                    {
                                                        //docs.DocRegDat = parsedDate.ToShortDateString();
                                                        docs.DocRegDat = parsedDate;
                                                        docs.DocRegDatSpecified = true;
                                                    }
                                                }
                                                //код
                                                if (xmlPdoc.LocalName == "PresentedDocumentModeCode")
                                                {
                                                    //TODO: Добавить справочник и заполнять по нему Тип документа  docs.DocsTyp
                                                    docs.DocCoDC28 = xmlPdoc.InnerXml.Substring(1);
                                                    //документ о происхождении
                                                    if (xmlPdoc.InnerXml.Substring(1, 2) == "06") docs.OriFlag = (HEAHEATypeTesIndMES18)1;
                                                }

                                            }
                                            listOfDocs.Add(docs);
                                            break;
                                        case "ESADGoodsPackaging":
                                            //упаковка                                            
                                            foreach (XmlElement xmlPack in elemenGoods)
                                            {
                                                if (xmlPack.LocalName == "PackagePalleteInformation")
                                                {
                                                    bool bUpk = false;
                                                    PACGS2Type pacs = new PACGS2Type();
                                                    foreach (XmlElement xmlPackage in xmlPack)
                                                    {

                                                        if (xmlPackage.LocalName == "InfoKindCode")
                                                            if (xmlPackage.InnerXml == "0")
                                                            {
                                                                bUpk = true;  //упаковка                                                                
                                                            }
                                                        if (xmlPackage.LocalName == "PalleteCode" && bUpk) pacs.KinOfPacGS23 = xmlPackage.InnerXml;
                                                        if (xmlPackage.LocalName == "PalleteQuantity" && bUpk) pacs.NumOfPacGS24 = xmlPackage.InnerXml;
                                                        //еще есть кол-во штук... пока пропустим.
                                                    }
                                                    if (pacs != null) listOfPacs.Add(pacs);
                                                }
                                            }
                                            break;
                                        case "ESADContainer":
                                            //Контейнера                                            
                                            foreach (XmlElement xmlCont in elemenGoods)
                                            {
                                                if (xmlCont.LocalName == "ContainerNumber")
                                                {
                                                    CONNR2Type container = new CONNR2Type();
                                                    foreach (XmlElement xmlContNum in xmlCont)
                                                    {
                                                        if (xmlContNum.LocalName == "ContainerIdentificaror") container.ConNumNR21 = xmlContNum.InnerXml;
                                                    }
                                                    listofConts.Add(container);
                                                }
                                            }
                                            break;
                                        default:
                                            break;
                                    }

                                }
                                if (listOfDocs.Count >= 0)
                                {
                                    tovarLV.PRODOCDC2 = listOfDocs.ToArray();
                                    listOfDocs.Clear();
                                };
                                if (listOfPacs.Count >= 0)
                                {
                                    tovarLV.PACGS2 = listOfPacs.ToArray();
                                    listOfPacs.Clear();
                                }
                                if (listofConts.Count >= 0)
                                {
                                    tovarLV.CONNR2 = listofConts.ToArray();
                                    listofConts.Clear();
                                }

                                if (!String.IsNullOrEmpty(key_API_Translator)) tovarLV.GooDesGDS23 = Translator.Translit(Translator.GetTranslate(tovarLV.GooDesGDS23, key_API_Translator));

                                listOfGoods.Add(tovarLV);

                            }
                        }
                    }
                }

                //TODO: cделать транслитерацию
                outObj.NOTPAR670 = fiiledperson;
                outObj.GOOITEGDS = listOfGoods.ToArray();

                outObj.TRACONCO1 = consignor;
                if (bConsigneeEqual)
                {
                    consignee.NamCE17 = declarant.NamTRE1;
                    consignee.CouCE125 = declarant.CouCodTRE1;
                    consignee.PosCodCE123 = declarant.PosCodTRE1;
                    consignee.StrAndNumCE122 = declarant.StrAndNumTRE1;
                    consignee.TINCE159 = declarant.TINTRE1;
                }
                outObj.TRACONCE1 = consignee;
                outObj.TRAREP = declarant;                
        }
            return outObj;

        }

        //парсер в наш формат
        private static object ConvertToESAD(string inputfile)
        {
            //десереализуем входной esadout            
            IcsIEType MyObj = new IcsIEType() { };
            MyObj = DeserializeXMLFileToObject<IcsIEType>(inputfile);

            ESADout_CUType eSADout = new ESADout_CUType() { };

            var gShipment = new ESADout_CUGoodsShipmentType() { };
            var goodsShipmentSubject = new GoodsShipmentSubjectDetailsType() { };
            var address = new RUAddressType() { };
            var listAddress = new List<RUAddressType>();
            //отпарвитель
            if (MyObj.TRACONCO1 != null)
            {
                goodsShipmentSubject.OrganizationName = MyObj.TRACONCO1.NamCO17;
                address.PostalCode = MyObj.TRACONCO1.PosCodCO123;
                address.CountryCode = MyObj.TRACONCO1.CouCO125;
                address.City = MyObj.TRACONCO1.CitCO124;
                address.StreetHouse = MyObj.TRACONCO1.StrAndNumCO122;                
                goodsShipmentSubject.SubjectAddressDetails = listAddress.ToArray();
                listAddress.Add(address);
                //goodsShipmentSubject.SubjectAddressDetails.Append(address);
                gShipment.ESADout_CUConsignor = goodsShipmentSubject;
                listAddress.Clear();
            }
            //получатель
            if (MyObj.TRACONCE1 != null)
            {
                goodsShipmentSubject.OrganizationName = MyObj.TRACONCE1.NamCE17;
                address.PostalCode = MyObj.TRACONCE1.PosCodCE123;
                address.CountryCode = MyObj.TRACONCE1.CouCE125;
                address.City = MyObj.TRACONCE1.CitCE124;                
                address.StreetHouse = MyObj.TRACONCE1.StrAndNumCE122;
                listAddress.Add(address);
                goodsShipmentSubject.SubjectAddressDetails = listAddress.ToArray();
                gShipment.ESADout_CUConsignee = goodsShipmentSubject;
                listAddress.Clear();
            }
            //декларант
            //TODO: сделать EqualIndicator если равны
            if (MyObj.TRAREP != null)
            {
                gShipment.ESADout_CUDeclarant.OrganizationName = MyObj.TRAREP.NamTRE1;
                address.PostalCode = MyObj.TRAREP.PosCodTRE1;
                address.CountryCode = MyObj.TRAREP.CouCodTRE1;
                address.City = MyObj.TRAREP.CitTRE1;
                address.StreetHouse = MyObj.TRAREP.StrAndNumTRE1;
                listAddress.Add(address);
                gShipment.ESADout_CUDeclarant.SubjectAddressDetails = listAddress.ToArray();                
                listAddress.Clear();
            }            

            eSADout.ESADout_CUGoodsShipment = gShipment;

            return eSADout;
        }

        private static void ErrorDtcimalConvertionNewMethod(XmlNode xmlNode)
        {
            Console.WriteLine("Не могу сконвертировать в десятичный тип '{0}'.", xmlNode.InnerXml);
        }

        public static void PrintXmlNodeValue(string field, XmlNode node)
        {
            if (node != null) Console.WriteLine($"{field} = {node.InnerXml}");
        }

        //
        public static T DeserializeXMLFileToObject<T>(string XmlFilename)
        {
            T returnObject = default(T);
            if (string.IsNullOrEmpty(XmlFilename)) return default(T);

            try
            {
                StreamReader xmlStream = new StreamReader(XmlFilename);
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                returnObject = (T)serializer.Deserialize(xmlStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return returnObject;
        }

    }
}


public static class MyExtensions
{
    public static string ShallowValue(this XElement xe)
    {
        return xe
               .Nodes()
               .OfType<XText>()
               .Aggregate(new StringBuilder(),
                          (s, c) => s.Append(c),
                          s => s.ToString());
    }
}

//переводчик 
public static class Translator
{
    public static string GetTranslate(string text, string key)
    {
      
        string url = "https://translate.yandex.net/api/v1.5/tr/translate";      

        using (var webClient = new WebClient())
        {               
#if DEBUG            
            key ="[API_KEY]";
#endif
            url = url + "?key=" + key + "&text=" + text+ "&lang=ru-lt";
           
            try
            {
                string result = webClient.DownloadString(url);                

                XmlSerializer serializer = new XmlSerializer(typeof(Translation));
                StringReader rdr = new StringReader(result);
                Translation resultingMessage = (Translation)serializer.Deserialize(rdr);

                if (resultingMessage.code == "200") return resultingMessage.text; else return text;
            }
            catch
            {
                return text;   
            }
        }        
    }

    public static string Translit(string str)
    {
        string[] lat_up = { "A", "B", "V", "G", "D", "E", "YO", "ZH", "Z", "I", "Y", "K", "L", "M", "N", "O", "P", "R", "S", "T", "U", "F", "KH", "TS", "CH", "SH", "SHCH", "\"", "Y", "'", "E", "YU", "YA" };
        string[] lat_low = { "a", "b", "v", "g", "d", "e", "yo", "zh", "z", "i", "y", "k", "l", "m", "n", "o", "p", "r", "s", "t", "u", "f", "kh", "ts", "ch", "sh", "shch", "\"", "y", "'", "e", "yu", "ya" };
        string[] rus_up = { "А", "Б", "В", "Г", "Д", "Е", "Ё", "Ж", "З", "И", "Й", "К", "Л", "М", "Н", "О", "П", "Р", "С", "Т", "У", "Ф", "Х", "Ц", "Ч", "Ш", "Щ", "Ъ", "Ы", "Ь", "Э", "Ю", "Я" };
        string[] rus_low = { "а", "б", "в", "г", "д", "е", "ё", "ж", "з", "и", "й", "к", "л", "м", "н", "о", "п", "р", "с", "т", "у", "ф", "х", "ц", "ч", "ш", "щ", "ъ", "ы", "ь", "э", "ю", "я" };
        for (int i = 0; i <= 32; i++)
        {
            str = str.Replace(rus_up[i], lat_up[i]);
           // str = str.Replace(rus_low[i], lat_low[i]);
        }
        return str;
    }

}


