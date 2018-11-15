
// OpenCVHelper.h

#pragma once

namespace OpenCVLibrary
{
	public ref class OpenCVHelper sealed
	{
	public:
		OpenCVHelper();

		// Image processing operators
		bool IsStopSign(
			Windows::Graphics::Imaging::SoftwareBitmap^ templateImage,
			Windows::Graphics::Imaging::SoftwareBitmap^ targetImage);

	private:
		// used only for the background subtraction operation
		
	};
}
