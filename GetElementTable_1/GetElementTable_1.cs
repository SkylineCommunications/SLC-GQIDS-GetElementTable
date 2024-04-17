namespace GetElementTable_1
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using Newtonsoft.Json.Linq;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.Exceptions;
    using Skyline.DataMiner.Net.Helper;
    using Skyline.DataMiner.Net.Messages;
    using Skyline.DataMiner.Net.Serialization;
    using Skyline.DataMiner.Protobuf.Shared.IdObjects.v1;

    [GQIMetaData(Name = "Get Element Table")]
    public class MyDataSource : IGQIDataSource, IGQIInputArguments, IGQIOnInit
    {
        private readonly GQIStringArgument dmaIdArgument = new GQIStringArgument("DMA ID") { IsRequired = true };
        private readonly GQIStringArgument elementIdArgument = new GQIStringArgument("Element ID") { IsRequired = true };
        private readonly GQIStringArgument tableIdArgument = new GQIStringArgument("Table ID") { IsRequired = false };
        private GQIDMS _dms;

        private string dmaId;
        private string elementId;
        private string tableId;

        private List<GQIColumn> _columns;

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;
            return new OnInitOutputArgs();
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[] { dmaIdArgument, elementIdArgument, tableIdArgument };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            _columns = new List<GQIColumn>();

            try
            {
                dmaId = args.GetArgumentValue(dmaIdArgument);
                elementId = args.GetArgumentValue(elementIdArgument);
                tableId = args.GetArgumentValue(tableIdArgument);

                var elementIdMessage = new GetElementByIDMessage
                {
                    DataMinerID = Convert.ToInt32(dmaId),
                    ElementID = Convert.ToInt32(elementId),
                };

                var elementInfo = (ElementInfoEventMessage)_dms.SendMessage(elementIdMessage);

                var protocol = elementInfo.Protocol;
                var version = elementInfo.ProtocolVersion;

                GetProtocolMessage getProtocolMessage = new GetProtocolMessage(protocol, version);
                var protocolInfo = (GetProtocolInfoResponseMessage)_dms.SendMessage(getProtocolMessage);

                var table = protocolInfo.FindParameter(Convert.ToInt32(tableId));

                if (table == null || !table.IsTable)
                {
                    throw new InvalidOperationException($"The specified parameter with ID {tableId} is not a valid table or does not exist.");
                }

                var allColumnIds = table.TableColumnDefinitions;

                foreach (var column in allColumnIds)
                {
                    string columnName = protocolInfo.GetParameterName(column.ParameterID);
                    var columnType = Convert.ToString(protocolInfo.FindParameter(column.ParameterID).InterpreteType);
                    var columnOptions = Convert.ToString(protocolInfo.FindParameter(column.ParameterID).Options);

                    if (column == null)
                    {
                        continue; // Skip this iteration if the column is null
                    }

                    if (columnType == "Double" && (columnOptions.Contains("Time") || columnOptions.Contains("Hours")))
                    {
                        _columns.Add(new GQIStringColumn(columnName));
                    }
                    else if (columnType == "Double")
                    {
                        _columns.Add(new GQIDoubleColumn(columnName));
                    }
                    else
                    {
                        _columns.Add(new GQIStringColumn(columnName));
                    }
                }
            }
            catch (DataMinerElementUnavailableException)
            {
                throw new DataMinerElementUnavailableException($"Unable to reach Element with DMA ID: {dmaId} and Element ID: {elementId}");
            }
            catch (FormatException ex)
            {
                throw new FormatException("DMA ID or Element ID was not in the correct format. Example: DMA ID: 477, Element ID: 178, Table ID: 100", ex);
            }
            catch (ArgumentNullException ex)
            {
                throw new ArgumentNullException("An unexpected error occurred while processing the arguments.", ex);
            }

            return new OnArgumentsProcessedOutputArgs();
        }

        public GQIColumn[] GetColumns()
        {
            return _columns.ToArray();
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            var rows = new List<GQIRow>();

            try
            {
                GetLiteElementInfo getLiteElementInfo = new GetLiteElementInfo
                {
                    DataMinerID = Convert.ToInt32(dmaId),
                    ElementID = Convert.ToInt32(elementId),
                };

                var elementInfoResponse = (LiteElementInfoEvent)_dms.SendMessage(getLiteElementInfo);

                var outputConfigTable = GetTable(_dms, elementInfoResponse, Convert.ToInt32(tableId));
                GetTableRows(rows, outputConfigTable);
            }
            catch (DataMinerElementUnavailableException)
            {
                throw new DataMinerElementUnavailableException("Unable to reach Element");
            }

            return new GQIPage(rows.ToArray())
            {
                HasNextPage = false,
            };
        }

        public static object[][] GetTable(GQIDMS dms, LiteElementInfoEvent response, int tableId)
        {
            var partialTableRequest = new GetPartialTableMessage
            {
                DataMinerID = response.DataMinerID,
                ElementID = response.ElementID,
                ParameterID = tableId,
            };

            var messageResponse = dms.SendMessage(partialTableRequest) as ParameterChangeEventMessage;
            if (messageResponse.NewValue.ArrayValue != null && messageResponse.NewValue.ArrayValue.Length > 0)
            {
                return BuildRows(messageResponse.NewValue.ArrayValue);
            }
            else
            {
                return new object[0][];
            }
        }

        public static object[][] BuildRows(ParameterValue[] columns)
        {
            int length1 = columns.Length;
            int length2 = 0;
            if (length1 > 0)
            {
                length2 = columns[0].ArrayValue.Length;
            }

            object[][] objArray;
            if (length1 > 0 && length2 > 0)
            {
                objArray = new object[length2][];
                for (int index = 0; index < length2; ++index)
                {
                    objArray[index] = new object[length1];
                }
            }
            else
            {
                objArray = new object[0][];
            }

            for (int index1 = 0; index1 < length1; ++index1)
            {
                ParameterValue[] arrayValue = columns[index1].ArrayValue;
                for (int index2 = 0; index2 < length2; ++index2)
                {
                    objArray[index2][index1] = arrayValue[index2].IsEmpty ? (object)null : arrayValue[index2].ArrayValue[0].InteropValue;
                }
            }

            return objArray;
        }

        private static string FormatTimeValue(object rawValue)
        {
            double value = Convert.ToDouble(rawValue);
            switch (value)
            {
                case 0:
                    return "00h";
                case -1:
                    return "Missing";
                default:
                    if (value > 0)
                    {
                        return FormatDuration(value);
                    }

                    break;
            }

            return Convert.ToString(rawValue);
        }

        private static string FormatDuration(double time)
        {
            int days = (int)(time / 24);
            int remainingHours = (int)(time % 24);
            int minutes = (int)((time - (int)time) * 60);

            return $"{days}d {remainingHours}h {minutes}m";
        }

        private void GetTableRows(List<GQIRow> rows, object[][] allLayoutsTable)
        {
            for (int i = 0; i < allLayoutsTable.Length; i++)
            {
                var deviceAllLayoutsRow = allLayoutsTable[i];
                var cells = new List<GQICell>();

                for (int j = 0; j < deviceAllLayoutsRow.Length; j++)
                {
                    string cellValue = Convert.ToString(deviceAllLayoutsRow[j]);
                    double numericValue;

                    if (_columns[j] is GQIDoubleColumn)
                    {
                        cells.Add(new GQICell { Value = Convert.ToDouble(deviceAllLayoutsRow[j]) });
                    }
                    else if (double.TryParse(cellValue, out numericValue))
                    {
                        string formattedTime = FormatTimeValue(numericValue);
                        cells.Add(new GQICell { Value = formattedTime });
                    }
                    else
                    {
                        cells.Add(new GQICell { Value = cellValue });
                    }
                }

                var row = new GQIRow(cells.ToArray());
                rows.Add(row);
            }
        }
    }
}
