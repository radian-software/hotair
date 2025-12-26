libhotair.so: libhotair.cpp
	$(CXX) libhotair.cpp -shared -fPIC -Isteam_headers -o libhotair.so
