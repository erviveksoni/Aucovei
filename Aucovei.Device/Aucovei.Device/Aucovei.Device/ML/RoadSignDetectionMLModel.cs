using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Aucovei.Device
{
    public sealed class RoadSignDetectionMLModelInput
    {
        public ImageFeatureValue Data; // BitmapPixelFormat: Bgra8, BitmapAlphaMode: Premultiplied, width: 227, height: 227
    }

    public sealed class RoadSignDetectionMLModelOutput
    {
        public TensorString ClassLabel; // shape(-1,1)
        public IList<IDictionary<string, float>> Loss = new List<IDictionary<string, float>>();
    }

    public sealed class RoadSignDetectionMLModel
    {
        private LearningModel model;
        private LearningModelSession session;
        private LearningModelBinding binding;
        public static async Task<RoadSignDetectionMLModel> CreateFromStreamAsync(IRandomAccessStreamReference stream)
        {
            RoadSignDetectionMLModel learningModel = new RoadSignDetectionMLModel();
            learningModel.model = await LearningModel.LoadFromStreamAsync(stream);
            learningModel.session = new LearningModelSession(learningModel.model);
            learningModel.binding = new LearningModelBinding(learningModel.session);
            return learningModel;
        }

        public static async Task<RoadSignDetectionMLModel> Createmlmodel(StorageFile file)
        {
            LearningModel learningModel = null;

            try
            {
                learningModel = await LearningModel.LoadFromStorageFileAsync(file);
            }
            catch (Exception e)
            {
                var exceptionStr = e.ToString();
                System.Console.WriteLine(exceptionStr);
                throw e;
            }
            var model = new RoadSignDetectionMLModel()
            {
                model = learningModel,
                session = new LearningModelSession(learningModel),
            };

            model.binding = new LearningModelBinding(model.session);

            return model;
        }

        public async Task<RoadSignDetectionMLModelOutput> EvaluateAsync(RoadSignDetectionMLModelInput input)
        {
            this.binding.Bind("data", input.Data);
            var result = await this.session.EvaluateAsync(this.binding, "0");
            var output = new RoadSignDetectionMLModelOutput();
            output.ClassLabel = result.Outputs["classLabel"] as TensorString;
            output.Loss = result.Outputs["loss"] as IList<IDictionary<string, float>>;
            return output;
        }
    }
}
