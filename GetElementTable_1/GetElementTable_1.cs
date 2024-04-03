namespace GetElementTable_1
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json.Linq;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Net.Messages;
    using Skyline.DataMiner.Net.Serialization;

    [GQIMetaData(Name = "Get Element Table")]
    public class MyDataSource : IGQIDataSource, IGQIInputArguments, IGQIOnInit
    {
        // Get input parameters from user (DMA ID, Element ID, Table ID, Columns IDs) (see GetParametersByProtocolAndView)

        // Use DMA ID + Element ID to find the correct element. Verify element exists

        // Use Table ID to find the table within the element. Verify it is a table

        // Using Columns IDs find the correct columns within the table. Verify columns exist ?

        // Use the columns to build the table. (see TAG-GQI-Retrieve Layouts)




        private GQIDMS _dms;

        private GQIStringArgument dmaIdArgument = new GQIStringArgument("DMA ID") { IsRequired = true };
        private GQIStringArgument elementIdArgument = new GQIStringArgument("Element ID") { IsRequired = true };
        private GQIStringArgument tableIdArgument = new GQIStringArgument("Table ID") { IsRequired = true };
        private GQIStringArgument columnsIdsArguments = new GQIStringArgument("Columns IDs") { IsRequired = false }; // ;-separated list of column ids

        private string dmaId;
        private string elementId;
        private string tableId;
        private List<int> columnIds;
        private GetElementByIDMessage elementInfo;

        // Used for testing for now, REMOVE LATER
        private string protocol;
        private string version;
        private GetProtocolInfoResponseMessage protocolInfo;


        private List<GQIColumn> _columns;

        public GQIColumn[] GetColumns()
        {
            return _columns.ToArray();
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[] { dmaIdArgument, elementIdArgument, tableIdArgument, columnsIdsArguments };
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            var rows = new List<GQIRow>();

            GetLiteElementInfo getLiteElementMessage = new GetLiteElementInfo(false)
            {
                DataMinerID = Convert.ToInt32(dmaId),
                ElementID = Convert.ToInt32(elementId),
            };

            var elementsInfo = _dms.SendMessages(getLiteElementMessage).Cast<LiteElementInfoEvent>().ToList();

            foreach (var element in elementsInfo)
            {
                List<GQICell> cells = new List<GQICell>();
                string elementKey = element.DataMinerID + "/" + element.ElementID;

                if(element.DataMinerID == Convert.ToInt32(dmaId) && element.ElementID == Convert.ToInt32(elementId))
                {
                    cells.Add(new GQICell() { Value = elementKey });
                    cells.Add(new GQICell() { Value = element.Name });

                    foreach(int column in columnIds)
                    {
                        GetParameterMessage getParameterMessage = new GetParameterMessage(element.DataMinerID, element.ElementID, column);
                        var response = (GetParameterResponseMessage)_dms.SendMessage(getParameterMessage);

                        cells.Add(new GQICell() { Value = response.Value.DoubleValue, DisplayValue = response.Value.DoubleValue + " " + protocolInfo.FindParameter(column).Units });
                    }

                }

                GQIRow myRow = new GQIRow(elementKey, cells.ToArray());
                rows.Add(myRow);
            }

            return new GQIPage(rows.ToArray())
            {
                HasNextPage = false,
            };

            /*
            try
            {
                var testElementInfo = new GetLiteElementInfo
                {
                    ProtocolName = "Skyline Squad Task Manager",
                    ProtocolVersion = "Production",
                };

                var someResponse = _dms.SendMessages(new DMSMessage[] { testElementInfo });

                foreach (var response in someResponse.Select(x => (LiteElementInfoEvent)x))
                {
                    var outputConfigTable = GetTable(_dms, response, Convert.ToInt32(tableId));
                    GetOutputMcsTableRows(rows, response, outputConfigTable);
                }

                // if no MCS in the system, gather MCM data
                if (rows.Count == 0)
                {
                    var mcmResponses = _dms.SendMessages(new DMSMessage[] { mcmRequest });
                    foreach (var response in mcmResponses.Select(x => (LiteElementInfoEvent)x))
                    {
                        var encoderConfigTable = GetTable(_dms, response, (int)McmTableId.DeviceEncoderConfig);
                        GetOutputMcmTableRows(rows, response, encoderConfigTable);
                    }
                }
            }
            catch (Exception e)
            {
                CreateDebugRow(rows, $"exception: {e}");
            }

            return new GQIPage(rows.ToArray())
            {
                HasNextPage = false,
            };
            */

        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            _columns = new List<GQIColumn>
            {
                new GQIStringColumn("First Column test"),
                new GQIStringColumn("Second Column test"),
            };

            dmaId = args.GetArgumentValue(dmaIdArgument);
            elementId = args.GetArgumentValue(elementIdArgument);
            tableId = args.GetArgumentValue(tableIdArgument);

            // REMOVE LATER
            protocol = "Skyline Squad Task Manager";
            version = "Production";

            columnIds = args.GetArgumentValue(columnsIdsArguments).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(Int32.Parse).ToList();

            //GetElementByIDMessage getElementByIDMessage = new GetElementByIDMessage(Convert.ToInt32(dmaId), Convert.ToInt32(elementId));
            //elementInfo = (GetLiteElementInfo)_dms.SendMessage(getElementByIDMessage);

            GetProtocolMessage getProtocolMessage = new GetProtocolMessage(protocol, version);
            protocolInfo = (GetProtocolInfoResponseMessage)_dms.SendMessage(getProtocolMessage);

            foreach(int column in columnIds)
            {
                string columnName = protocolInfo.GetParameterName(column);
                _columns.Add(new GQIDoubleColumn(columnName));
            }

            return new OnArgumentsProcessedOutputArgs();
        }

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;
            return new OnInitOutputArgs();
        }

        //public static object[][] GetTable(GQIDMS _dms,, LiteElementInfoEvent response, int tableId)
        //{
        //    var partialTableRequest = new GetPartialTableMessage
        //    {
        //        DataMinerID = response.DataMinerID,
        //        ElementID = response.ElementID,
        //        ParameterID = tableId,
        //    };

        //    var messageResponse = _dms.SendMessage(partialTableRequest) as ParameterChangeEventMessage;
        //    if(messageResponse.NewValue.ArrayValue != null && messageResponse.NewValue.ArrayValue.Length > 0)
        //    {
        //        return BuildRows(messageResponse.NewValue.ArrayValue);
        //    }
        //    else
        //    {
        //        return new object[0][];
        //    }
        //}
        
        //public static object[][] BuildRows(ParameterValue[] columns)
        //{
        //    int length1 = columns.Length;
        //    int length2 = 0;
        //    if (length1 > 0)
        //        length2 = columns[0].ArrayValue.Length;
        //    object[][] objArray;
        //    if (length1 > 0 && length2 > 0)
        //    {
        //        objArray = new object[length2][];
        //        for (int index = 0; index < length2; ++index)
        //            objArray[index] = new object[length1];
        //    }
        //    else
        //    {
        //        objArray = new object[0][];
        //    }

        //    for (int index1 = 0; index1 < length1; ++index1)
        //    {
        //        ParameterValue[] arrayValue = columns[index1].ArrayValue;
        //        for (int index2 = 0; index2 < length2; ++index2)
        //            objArray[index2][index1] = arrayValue[index2].IsEmpty ? (object)null : arrayValue[index2].ArrayValue[0].InteropValue;
        //    }

        //    return objArray;
        //}

        //private static void CreateDebugRow(List<GQIRow> rows, string message)
        //{
        //    var debugCells = new[]
        //    {
        //        new GQICell { Value = message },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null},
        //        new GQICell { Value = null},
        //        new GQICell { Value = null },
        //        new GQICell { Value = null },
        //        new GQICell { Value = null},
        //        new GQICell { Value = null},
        //        new GQICell { Value = null},
        //        new GQICell { Value = null},
        //        new GQICell { Value = null},
        //    };

        //    var row = new GQIRow(debugCells);
        //    rows.Add(row);
        //}
    }
}
