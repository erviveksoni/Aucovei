
#include "pch.h"
#include "OpenCVHelper.h"
#include "MemoryBuffer.h"
#include <iostream>

using namespace Microsoft::WRL;

using namespace OpenCVLibrary;
using namespace Platform;
using namespace Windows::Graphics::Imaging;
using namespace Windows::Storage::Streams;
using namespace Windows::Foundation;

using namespace cv;
using namespace std;

OpenCVHelper::OpenCVHelper()
{
}

void OpenCVHelper::GetLargestRedObjectCrop(SoftwareBitmap^ inputImg, SoftwareBitmap^ output)
{
	Mat input, outputMat;
	if (!(TryConvert(inputImg, input) && TryConvert(output, outputMat)))
	{
		return;
	}

	Mat maskImage = cv::Mat::zeros(input.size(), CV_8UC1);
	Mat bgr = input;

	// inverting the BGR image
	Mat bgr_inv = ~bgr;

	// converting to HSV color space
	Mat hsv_inv;
	cvtColor(bgr_inv, hsv_inv, COLOR_BGR2HSV);

	// detecting Cyan regions in iverted image which would be reddish regions in the original image
	Mat mask;
	//inRange(hsv_inv, Scalar(80, 50, 70), Scalar(100, 255, 255), mask); // Cyan is 90
	inRange(hsv_inv, Scalar(80, 100, 100), Scalar(100, 255, 255), mask); // Cyan is 90
	//imshow("Red", mask);
	//waitKey(0);

	// Find & process the contours
	vector< vector< cv::Point> > contours;
	vector <Vec4i> heirarchy;
	findContours(mask, contours, heirarchy, RETR_CCOMP, CHAIN_APPROX_SIMPLE);

	// find the largest squarish contour
	cv::Rect largestSquareRect = cv::Rect(0, 0, 0, 0);
	for (int i = 0; i < (int)contours.size(); ++i)
	{
		//double area = contourArea(contours[i]);
		cv::Rect r = boundingRect(contours[i]);
		float aspect_ratio = r.width / r.height;
		if (aspect_ratio > 0.5 && aspect_ratio < 1.5)
		{
			if (largestSquareRect.width * largestSquareRect.height < r.width * r.height)
				largestSquareRect = r;
		}
	}

	Mat cropped;
	// if found a valid region, extract it from the original image
	if (largestSquareRect.width > 0 && largestSquareRect.height > 0)
	{
		cropped = input(largestSquareRect);
	}
	else
		input.copyTo(cropped);

	cv::resize(cropped, outputMat, cv::Size(output->PixelWidth, output->PixelHeight));
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