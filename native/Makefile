# Builds libhaikusharp.so, the C shim over BeAPI (Application Kit + Interface Kit).
# Must be built ON Haiku (against real libbe.so + Haiku headers) -- there is
# no cross-compilation story here yet.

CXX ?= g++
CXXFLAGS ?= -O2 -fPIC -Wall -Wextra -Iinclude
LDFLAGS ?= -lbe

SRCS := src/hs_application.cpp src/hs_message.cpp src/hs_window.cpp src/hs_view.cpp src/hs_control.cpp src/hs_button.cpp src/hs_text_control.cpp src/hs_checkbox.cpp src/hs_radio_button.cpp src/hs_slider.cpp
OBJS := $(SRCS:.cpp=.o)
TARGET := libhaikusharp.so

.PHONY: all clean

all: $(TARGET)

$(TARGET): $(OBJS)
	$(CXX) -shared -o $@ $(OBJS) $(LDFLAGS)

%.o: %.cpp include/hs_types.h include/hs_message.h include/hs_application.h include/hs_window.h include/hs_view.h include/hs_control.h include/hs_button.h include/hs_text_control.h include/hs_checkbox.h include/hs_radio_button.h include/hs_slider.h
	$(CXX) $(CXXFLAGS) -c -o $@ $<

clean:
	rm -f $(OBJS) $(TARGET)
