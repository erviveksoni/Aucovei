
#include "pch.h"
#include "OpenCVHelper.h"
#include "MemoryBuffer.h"
#include <iostream>
#include <ctime>

using namespace Microsoft::WRL;

using namespace OpenCVLibrary;
using namespace Platform;
using namespace Windows::Graphics::Imaging;
using namespace Windows::Storage::Streams;
using namespace Windows::Foundation;

using namespace cv;
using namespace std;
using namespace cv::xfeatures2d;

int THRESHOLD = 7100;

OpenCVHelper::OpenCVHelper()
{
}

// computes mean square error between two n-d matrices (same size).
// lower mse means higher similarity
static double meanSquareError(const Mat &img1, const Mat &img2) {
	Mat s1;
	absdiff(img1, img2, s1);   // |img1 - img2|
	s1.convertTo(s1, CV_32F);  // cannot make a square on 8 bits
	s1 = s1.mul(s1);           // |img1 - img2|^2
	Scalar s = sum(s1);        // sum elements per channel
	double sse = s.val[0] + s.val[1] + s.val[2];  // sum channels
	double mse = sse / (double)(img1.channels() * img1.total());
	return mse;
}

bool OpenCVHelper::IsStopSign(SoftwareBitmap^ templateImage, SoftwareBitmap^ targetImage)
{
	Mat image, prototypeImg;
	if (!(TryConvert(templateImage, prototypeImg) && TryConvert(targetImage, image)))
	{
		return false;
	}

	if (!image.data || !prototypeImg.data)
	{
		std::cout << " --(!) Error reading images " << std::endl; return false;
	}

	int width = 200;
	int height = width * image.rows / image.cols;
	resize(image, image, cv::Size(width, height));

	std::vector<KeyPoint> keypoints1, keypoints2;
	Mat descriptors1, descriptors2;
	int minHessian = 400;
	Ptr<SURF> detector = SURF::create(minHessian);
	detector->detectAndCompute(prototypeImg, noArray(), keypoints1, descriptors1);
	detector->detectAndCompute(image, noArray(), keypoints2, descriptors2);
	//-- Step 2: Matching descriptor vectors with a FLANN based matcher
	// Since SURF is a floating-point descriptor NORM_L2 is used
	Ptr<DescriptorMatcher> matcher = DescriptorMatcher::create(DescriptorMatcher::FLANNBASED);
	std::vector< std::vector<DMatch> > knn_matches;
	matcher->knnMatch(descriptors1, descriptors2, knn_matches, 2);

	//-- Filter matches using the Lowe's ratio test
	const float ratio_thresh = 0.70f;
	std::vector<DMatch> good_matches;
	for (size_t i = 0; i < knn_matches.size(); i++)
	{
		if (knn_matches[i][0].distance < ratio_thresh * knn_matches[i][1].distance)
		{
			good_matches.push_back(knn_matches[i][0]);
		}
	}

	float matchpercentage = (good_matches.size() * 100) / (knn_matches.size() + 0.001);
	if (matchpercentage > 20.0) {
		return true;
	}

	return false;
}

bool OpenCVHelper::TryConvert(SoftwareBitmap^ from, Mat& convertedMat)
{
	unsigned char* pPixels = nullptr;
	unsigned int capacity = 0;
	if (!GetPointerToPixelData(from, &pPixels, &capacity))
	{
		return false;
	}

	Mat mat(from->PixelHeight,
		from->PixelWidth,
		CV_8UC4, // assume input SoftwareBitmap is BGRA8
		(void*)pPixels);

	// shallow copy because we want convertedMat.data = pPixels
	// don't use .copyTo or .clone
	convertedMat = mat;
	return true;
}

bool OpenCVHelper::GetPointerToPixelData(SoftwareBitmap^ bitmap, unsigned char** pPixelData, unsigned int* capacity)
{
	BitmapBuffer^ bmpBuffer = bitmap->LockBuffer(BitmapBufferAccessMode::ReadWrite);
	IMemoryBufferReference^ reference = bmpBuffer->CreateReference();

	ComPtr<IMemoryBufferByteAccess> pBufferByteAccess;
	if ((reinterpret_cast<IInspectable*>(reference)->QueryInterface(IID_PPV_ARGS(&pBufferByteAccess))) != S_OK)
	{
		return false;
	}

	if (pBufferByteAccess->GetBuffer(pPixelData, capacity) != S_OK)
	{
		return false;
	}
	return true;
}