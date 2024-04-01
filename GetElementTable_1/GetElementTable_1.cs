namespace GetElementTable_1
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Automation;

    [GQIMetaData(Name = "Get Element Table")]
    public class MyDataSource : IGQIDataSource, IGQIInputArguments, IGQIOnInit
    {
        private GQIDMS _dms;

        private GQIStringArgument dmaIdArgument = new GQIStringArgument("DMA ID") { IsRequired = true };
        private GQIStringArgument elementIdArgument = new GQIStringArgument("Element ID") { IsRequired= true };
        private GQIStringArgument tableIdArgument = new GQIStringArgument("Table ID") { IsRequired = true };
        private GQIStringArgument columnsIdsArguments = new GQIStringArgument("Columns IDs") { IsRequired = true };

        private string dmaId;
        private string elementId;
        private string tableId;
        private List<string> columnIds;


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





            return new GQIPage(rows.ToArray())
            {
                HasNextPage = false,
            };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            throw new NotImplementedException();
        }

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;
            return new OnInitOutputArgs();
        }
    }
}
