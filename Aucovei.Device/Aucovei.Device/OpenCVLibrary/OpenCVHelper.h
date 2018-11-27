
// OpenCVHelper.h

#pragma once
#include <opencv2\core\core.hpp>
#include <opencv2\imgproc\imgproc.hpp>
#include <opencv2\video.hpp>

namespace OpenCVLibrary
{
	public ref class OpenCVHelper sealed
	{
	public:
		OpenCVHelper();
		void GetLargestRedObjectCrop(
			Windows::Graphics::Imaging::SoftwareBitmap^ inputImg,
			Windows::Graphics::Imaging::SoftwareBitmap^ output);
	private:
		// used only for the background subtraction operation
		bool GetPointerToPixelData(Windows::Graphics::Imaging::SoftwareBitmap^ bitmap,
			unsigned char** pPixelData, unsigned int* capacity);

		bool TryConvert(Windows::Graphics::Imaging::SoftwareBitmap^ from, cv::Mat& convertedMat);
	};
}
