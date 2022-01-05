using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SegRaster
{
    internal class SegRasterButton : Button
    {
        protected override async void OnClick()
        {

            var mapView = MapView.Active;
            IReadOnlyList<Layer> selectedLayerList = mapView.GetSelectedLayers();

            for (int cur = 0; cur < selectedLayerList.Count(); cur++)
            {
                Layer firstSelectedLayer = selectedLayerList[cur];
                await SegBandRepartitionAsyc(firstSelectedLayer);
            }
        }

        /// <summary>
        /// 剪切选择栅格的最小有效值范围，并按每个波段输出
        /// </summary>
        /// <param name="firstSelectedLayer"></param>
        /// <returns></returns>
        public static async Task SegBandRepartitionAsyc(Layer firstSelectedLayer)
        {
            try
            {
                await QueuedTask.Run(() =>
                {

                    RasterLayer currentRasterLayer = firstSelectedLayer as RasterLayer;
                    Raster inputRaster = currentRasterLayer.GetRaster();
                    BasicRasterDataset basicRasterDataset = inputRaster.GetRasterDataset();

                    string layer_name = currentRasterLayer.Name;
                    string[] strs1 = layer_name.Split('.');
                    string name = strs1[0];

                    if (!(basicRasterDataset is RasterDataset))
                    {
                        MessageBox.Show("No Raster Layers selected. Please select one Raster layer.");
                        return;
                    }
                    RasterDataset rasterDataset = basicRasterDataset as RasterDataset;
                    Raster workingRater = rasterDataset.CreateFullRaster();

                    string path1 = basicRasterDataset.GetDatastore().GetPath().LocalPath;

                    //输出路径
                    string path = path1 + name;
                    DirectoryInfo dir = new DirectoryInfo(path);

                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    FileSystemConnectionPath outputConnectionPath = new FileSystemConnectionPath(
                      new System.Uri(path), FileSystemDatastoreType.Raster);
                    FileSystemDatastore outputFileSytemDataStore = new FileSystemDatastore(outputConnectionPath);
                    RasterStorageDef rasterStorageDef = new RasterStorageDef();
                    rasterStorageDef.SetPyramidLevel(0);


                    //确定改变的栅格大小，计算最大，最小值
                    int width = workingRater.GetWidth();
                    int height = workingRater.GetHeight();
                    int minCol = width, minRow = height, maxCol = 0, maxRow = 0;
                    int plane_num = workingRater.GetBandCount();

                    int pbWidth = 0, pbHeight = 0;
                    Envelope env1 = null;

                    for (int plane = 0; plane < plane_num; plane++)
                    {

                        for (int i = 0; i < width; i++)
                        {
                            for (int j = 0; j < height; j++)
                            {
                                double value = (double)workingRater.GetPixelValue(plane, i, j);
                                if (value != 0)
                                {
                                    minRow = Math.Min(minRow, j);
                                    minCol = Math.Min(minCol, i);
                                    maxRow = Math.Max(maxRow, j);
                                    maxCol = Math.Max(maxCol, i);
                                }
                            }
                        }


                    }

                    pbWidth = maxCol - minCol + 1;
                    pbHeight = maxRow - minRow + 1;

                    Envelope env = workingRater.GetExtent();
                    double res_x = (env.XMax - env.XMin) / width;
                    double res_y = (env.YMax - env.YMin) / height;

                    double xMin = env.XMin + res_x * minCol;
                    double yMin = env.YMax - res_y * maxRow - res_y;
                    double xMax = env.XMin + res_x * maxCol + res_x;
                    double yMax = env.YMax - res_y * minRow;
                    if (xMax < xMin || yMax < yMin || (xMax == xMin && yMax == yMin))
                    {
                        return;
                    }
                    env1 = EnvelopeBuilder.CreateEnvelope(xMin, yMin, xMax, yMax, SpatialReferences.WGS84);



                    //输出每个波段tif
                    int num = workingRater.GetBandCount();
                    for (int i = 0; i < num; i++)
                    {
                        // Calculate the width and height of the pixel block to create.

                        Raster raster = workingRater.GetBand(i).CreateDefaultRaster();

                        raster.SetHeight(pbHeight);
                        raster.SetWidth(pbWidth);
                        raster.SetExtent(env1);
                        raster.SetPixelType(RasterPixelType.DOUBLE);

                        PixelBlock currentPixelBlock = raster.CreatePixelBlock(pbWidth, pbHeight);

                        Array p = currentPixelBlock.GetPixelData(0, true);

                        for (int j = 0; j < pbWidth; j++)
                        {
                            for (int k = 0; k < pbHeight; k++)
                            {
                                double value = (double)workingRater.GetPixelValue(i, minCol + j, minRow + k);
                                p.SetValue(value, j, k);
                            }
                        }

                        currentPixelBlock.SetPixelData(0, p);
                        raster.Write(0, 0, currentPixelBlock);
                        raster.Refresh();

                        string outputname = i + ".tif";
                        raster.SaveAs(outputname, outputFileSytemDataStore, "TIFF", rasterStorageDef);

                    }

                });
            }
            catch (Exception e)
            {
                MessageBox.Show("Exception caught while trying to add layer: " + e.Message);
                return;
            }
        }

    }
}
